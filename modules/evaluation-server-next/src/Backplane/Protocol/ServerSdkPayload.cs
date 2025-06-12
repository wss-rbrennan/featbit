//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json.Nodes;
//using System.Threading.Tasks;

//namespace Backplane.Protocol
//{
//    public class ServerSdkPayload
//    {
//        public string EventType { get; set; }

//        public IEnumerable<JsonObject> FeatureFlags { get; set; }

//        public IEnumerable<JsonObject> Segments { get; set; }

//        public ServerSdkPayload(string eventType, IEnumerable<JsonObject> featureFlags, IEnumerable<JsonObject> segments)
//        {
//            EventType = eventType;
//            FeatureFlags = featureFlags;
//            Segments = segments;
//        }

//        public bool IsEmpty()
//        {
//            return EventType == DataSyncEventTypes.Patch && !FeatureFlags.Any() && !Segments.Any();
//        }
//    }
//}
