using System.ComponentModel.DataAnnotations;

namespace NMAC.Core;

public class DeveloperBasicAuthOptions
{
    public const string DeveloperEndpointsPolicy = "DeveloperEndpointsBasicAuth";

    [Required]
    public required string Username { get; set; }

    [Required]
    public required string Password { get; set; }
}