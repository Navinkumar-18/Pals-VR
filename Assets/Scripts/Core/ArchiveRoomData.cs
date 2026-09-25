using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Data-driven placement of one exhibit station inside the Archive Room.
    /// The station binds to an ArchiveRecord by stable id; nothing exhibit
    /// specific is hard-coded in scripts.
    /// </summary>
    [Serializable]
    public class ExhibitStation
    {
        public string exhibitId;
        public string displayType; // "" = auto from record type | manuscript|book|document|letter|photograph|speech|audio|video|event
        public float[] position = { 0f, 0.45f, 2.6f };
        public float[] rotation = { 0f, 180f, 0f };
        public float scale = 1f;

        public Vector3 Position
        {
            get { return new Vector3(Get(position, 0, 0f), Get(position, 1, 0.45f), Get(position, 2, 2.6f)); }
        }

        public Quaternion Rotation
        {
            get { return Quaternion.Euler(Get(rotation, 0, 0f), Get(rotation, 1, 180f), Get(rotation, 2, 0f)); }
        }

        private static float Get(float[] values, int index, float fallback)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return fallback;
            }

            return values[index];
        }
    }

    [Serializable]
    public class ArchiveRoomConfig
    {
        public string roomName = "Archive Room (Demo)";
        public float[] accentColor = { 0.55f, 0.35f, 0.12f };
        public float[] boardColor = { 0.20f, 0.42f, 0.55f };
        public List<ExhibitStation> stations = new List<ExhibitStation>();

        public Color Accent
        {
            get { return ColorFrom(accentColor, new Color(0.55f, 0.35f, 0.12f)); }
        }

        public Color Board
        {
            get { return ColorFrom(boardColor, new Color(0.20f, 0.42f, 0.55f)); }
        }

        private static Color ColorFrom(float[] values, Color fallback)
        {
            if (values == null || values.Length < 3)
            {
                return fallback;
            }

            return new Color(values[0], values[1], values[2]);
        }
    }

    [Serializable]
    public class ArchiveRoomData
    {
        public ArchiveRoomConfig room;
    }

    /// <summary>
    /// Loads the Archive Room layout from Resources/Data/archive_room.json.
    /// Falls back to a small built-in layout (clearly labelled demo) if the
    /// resource is missing, so the room never hard-fails.
    /// </summary>
    public static class ArchiveRoomDataLoader
    {
        private const string ResourcePath = "Data/archive_room";

        private static ArchiveRoomConfig s_config;

        public static ArchiveRoomConfig Load()
        {
            if (s_config != null)
            {
                return s_config;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset != null)
            {
                try
                {
                    ArchiveRoomData data = JsonUtility.FromJson<ArchiveRoomData>(asset.text);
                    if (data != null && data.room != null && data.room.stations != null
                        && data.room.stations.Count > 0)
                    {
                        s_config = data.room;
                        Debug.Log("[AmbedkarHeritage] Archive room loaded: " + s_config.roomName
                                  + " with " + s_config.stations.Count + " station(s).");
                        return s_config;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[AmbedkarHeritage] Failed to parse archive_room.json: " + exception.Message);
                }
            }

            s_config = BuiltInFallback();
            Debug.LogWarning("[AmbedkarHeritage] archive_room.json missing/invalid; using built-in demo layout.");
            return s_config;
        }

        private static ArchiveRoomConfig BuiltInFallback()
        {
            var config = new ArchiveRoomConfig { roomName = "Archive Room (built-in fallback)" };
            config.stations.Add(S("AMB-SAM-001", "manuscript", -2.7f));
            config.stations.Add(S("AMB-SAM-002", "document", 0f));
            config.stations.Add(S("AMB-SAM-003", "photograph", 2.7f));
            return config;
        }

        private static ExhibitStation S(string id, string displayType, float x)
        {
            return new ExhibitStation
            {
                exhibitId = id,
                displayType = displayType,
                position = new[] { x, 0.45f, 2.6f },
                rotation = new[] { 0f, 180f, 0f },
                scale = 1f
            };
        }
    }
}