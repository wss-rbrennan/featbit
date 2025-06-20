using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Featbit.Examples.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlagViewController : ControllerBase
    {
        private readonly IFbClient _fbClient;

        public FlagViewController(IFbClient fbClient)
        {
            _fbClient = fbClient;
        }

        [HttpGet()]
        public async Task<IActionResult> GetFlagValueAsync(string userId = "test-web-user")
        {

            try
            {
                var flagKey = new List<string>()
                {
                    "test-flag1",
                    "demo-flag"
                };

                var user = FbUser.Builder(Guid.NewGuid().ToString()).Name(userId).Build();

                var results = new Dictionary<string, string>();



                foreach (var key in flagKey)
                {
                    try
                    {

                        var flagValue = _fbClient.BoolVariation(key, user);
                        results.Add(key, flagValue.ToString());

                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error retrieving flag value for {key}: {ex.Message}");
                    }
                }
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var resultJson = System.Text.Json.JsonSerializer.Serialize(results, options);

                return Ok(resultJson);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving flag value: {ex.Message}");
            }
        }
    }
}
