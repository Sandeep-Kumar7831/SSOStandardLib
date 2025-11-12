namespace MauiSso.Plugin.Configuration
{
    /// <summary>
    /// SSO Configuration - moved to plugin to avoid dependency in MAUI app
    /// </summary>
    public class SsoConfiguration
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public string Scope { get; set; } = "openid profile email offline_access";
        public string RedirectUri { get; set; } = string.Empty;
        public string? PostLogoutRedirectUri { get; set; }
        public bool EnableDPoP { get; set; } = false;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Authority) &&
                   !string.IsNullOrWhiteSpace(ClientId) &&
                   !string.IsNullOrWhiteSpace(RedirectUri);
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Authority))
                errors.Add("Authority is required");

            if (string.IsNullOrWhiteSpace(ClientId))
                errors.Add("ClientId is required");

            if (string.IsNullOrWhiteSpace(RedirectUri))
                errors.Add("RedirectUri is required");

            return errors;
        }
    }
}