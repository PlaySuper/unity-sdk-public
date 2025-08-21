using System.Threading.Tasks;
using PlaySuperUnity.FeatureFlags;

namespace PlaySuperUnity.Tests
{
    /// <summary>
    /// Mock GameManager for testing purposes
    /// </summary>
    internal static class GameManager
    {
        public static async Task<GameData> GetGameData()
        {
            // Return mock game data for testing
            return new GameData
            {
                id = "test-game-id",
                name = "Test Game",
                studioId = "test-studio-id",
                platform = new string[] { "iOS", "Android" },
                studio = new Studio
                {
                    organizationId = "test-org-id",
                    organization = new Organization
                    {
                        handle = "test-org-handle"
                    }
                }
            };
        }
    }
}