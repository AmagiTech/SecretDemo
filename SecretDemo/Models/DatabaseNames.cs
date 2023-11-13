namespace SecretDemo.Models
{
    public static class DatabaseNames
    {
        private static string _sampleDatabase = null;
        public static string SampleDatabase
        {
            get
            {
                if (_sampleDatabase == null)
                {
                    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "";
                    _sampleDatabase = $"SampleDatabase{environmentName}";
                }
                return _sampleDatabase;
            }
        }
    }
}
