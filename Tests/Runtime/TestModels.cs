using System;
using UnityEngine;

namespace PlaySuperUnity.FeatureFlags
{
    // Mock classes for testing purposes
    [Serializable]
    internal class GameData
    {
        public string id { get; set; }
        public string name { get; set; }
        public string studioId { get; set; }
        public string[] platform { get; set; }
        public Studio studio { get; set; }
    }

    [Serializable]
    internal class Studio
    {
        public string organizationId { get; set; }
        public Organization organization { get; set; }
    }

    [Serializable]
    internal class Organization
    {
        public string handle { get; set; }
    }
}