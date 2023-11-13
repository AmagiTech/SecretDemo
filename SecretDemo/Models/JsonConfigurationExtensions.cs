using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Text.Json;

namespace SecretDemo.Models
{
    public static class JsonSecretConfigurationExtensions
    {
        public static IConfigurationBuilder DecryptSecretJsonFile(this IConfigurationBuilder builder, bool optional, bool reloadOnChange)
        {
            var secret = builder.Sources.FirstOrDefault(q =>
            {
                var secret = (q as JsonConfigurationSource);
                if (secret != null && secret.Path == "secrets.json")
                {
                    return true;
                }
                return false;
            });
            if (secret != null)
            {
                var s = (secret as JsonConfigurationSource);
                var root = ((PhysicalFileProvider)(s.FileProvider)).Root;
                var path = Path.Combine(root, s.Path);
                builder.Sources.Remove(secret);
                return builder.AddSecretJsonFile(path, optional, reloadOnChange);
            }
            return builder;
        }

        public static IConfigurationBuilder AddSecretJsonFile(this IConfigurationBuilder builder, string path)
        {
            return AddSecretJsonFile(builder, provider: null, path: path, optional: false, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddSecretJsonFile(this IConfigurationBuilder builder, string path, bool optional)
        {
            return AddSecretJsonFile(builder, provider: null, path: path, optional: optional, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddSecretJsonFile(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange)
        {
            return AddSecretJsonFile(builder, provider: null, path: path, optional: optional, reloadOnChange: reloadOnChange);
        }

        public static IConfigurationBuilder AddSecretJsonFile(this IConfigurationBuilder builder, IFileProvider? provider, string path, bool optional, bool reloadOnChange)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Invalid path", nameof(path));
            }

            return builder.AddSecretJsonFile(s =>
            {
                s.FileProvider = provider;
                s.Path = path;
                s.Optional = optional;
                s.ReloadOnChange = reloadOnChange;
                s.ResolveFileProvider();
            });
        }

        public static IConfigurationBuilder AddSecretJsonFile(this IConfigurationBuilder builder, Action<JsonSecretConfigurationSource>? configureSource)
            => builder.Add(configureSource);

    }
    public class JsonSecretConfigurationSource : JsonConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new JsonSecretConfigurationProvider(this);
        }
    }

    public class JsonSecretConfigurationProvider : FileConfigurationProvider
    {
        /// <summary>
        /// Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public JsonSecretConfigurationProvider(JsonConfigurationSource source) : base(source) { }

        /// <summary>
        /// Loads the JSON data from a stream.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        public override void Load(Stream stream)
        {
            try
            {
                Data = JsonConfigurationFileParser.Parse(stream);
            }
            catch (JsonException e)
            {
                throw new FormatException("Invalid format", e);
            }
        }

        internal sealed class JsonConfigurationFileParser
        {
            private JsonConfigurationFileParser() { }

            private readonly Dictionary<string, string?> _data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            private readonly Stack<string> _paths = new Stack<string>();

            public static IDictionary<string, string?> Parse(Stream input)
                => new JsonConfigurationFileParser().ParseStream(input);

            private Dictionary<string, string?> ParseStream(Stream input)
            {
                var jsonDocumentOptions = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                };

                using (var reader = new StreamReader(input))
                using (JsonDocument doc = JsonDocument.Parse(reader.ReadToEnd(), jsonDocumentOptions))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw new FormatException($"Invalid format: {doc.RootElement.ValueKind}");
                    }
                    VisitObjectElement(doc.RootElement);
                }

                return _data;
            }

            private void VisitObjectElement(JsonElement element)
            {
                var isEmpty = true;

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    isEmpty = false;
                    EnterContext(property.Name);
                    VisitValue(property.Value);
                    ExitContext();
                }

                SetNullIfElementIsEmpty(isEmpty);
            }

            private void VisitArrayElement(JsonElement element)
            {
                int index = 0;

                foreach (JsonElement arrayElement in element.EnumerateArray())
                {
                    EnterContext(index.ToString());
                    VisitValue(arrayElement);
                    ExitContext();
                    index++;
                }

                SetNullIfElementIsEmpty(isEmpty: index == 0);
            }

            private void SetNullIfElementIsEmpty(bool isEmpty)
            {
                if (isEmpty && _paths.Count > 0)
                {
                    _data[_paths.Peek()] = null;
                }
            }

            private void VisitValue(JsonElement value)
            {
                Debug.Assert(_paths.Count > 0);

                switch (value.ValueKind)
                {
                    case JsonValueKind.Object:
                        VisitObjectElement(value);
                        break;

                    case JsonValueKind.Array:
                        VisitArrayElement(value);
                        break;

                    case JsonValueKind.Number:
                    case JsonValueKind.String:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                    case JsonValueKind.Null:
                        string key = _paths.Peek();
                        if (_data.ContainsKey(key))
                        {
                            throw new FormatException($"Error_KeyIsDuplicated, {key}");
                        }
                        if (value.ValueKind == JsonValueKind.String)
                            _data[key] = LocalEncryption.AesDecrypt(value.ToString());
                        else _data[key] = value.ToString();
                        break;

                    default:
                        throw new FormatException($"Error_UnsupportedJSONToken, {value.ValueKind}");
                }
            }

            private void EnterContext(string context) =>
                _paths.Push(_paths.Count > 0 ?
                    _paths.Peek() + ConfigurationPath.KeyDelimiter + context :
                    context);

            private void ExitContext() => _paths.Pop();
        }
    }

}
