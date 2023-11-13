namespace SecretDemo.Models
{
    public static class ConfigurationHelper
    {
        public static IEnumerable<string> GetSecretConfigurations(IConfiguration configurationBuilder, string configSectionKey)
        {
            var result = new List<string>();
            var section = configurationBuilder.GetSection(configSectionKey);
            var children = section.GetChildren();
            if (children?.Any() ?? false)
            {
                foreach (var item in children)
                {
                    var path = item?.Value ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(path) && path.StartsWith('.'))
                        path = Path.Combine(Directory.GetCurrentDirectory(), path);
                    if (File.Exists(path))
                        result.Add(path);
                }
            }
            return result;
        }
    }
}
