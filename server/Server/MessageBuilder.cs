using MUSE.Server.Messages;
using MUSE.Server.Middleware;
using MUSE.Server.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MUSE.Server
{
    public class MessageBuilder
    {
        private static JsonSerializerSettings _serializerSettings;

        static MessageBuilder()
        {
            _serializerSettings = new JsonSerializerSettings();
            _serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }
               
        public static Message BuildParticipantListMessage(List<Client> clients)
        {
            List<SessionUserList.User> participants = new List<SessionUserList.User>();

            foreach (var client in clients)
            {
                participants.Add(new SessionUserList.User
                {
                    Username = client.Name,
                    Color = client.Color
                });
            }

            return new SessionUserList
            {
                Users = participants
            };
        }
    }
}
