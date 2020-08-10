using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;

namespace FezEngine.Mod {
    public static class YamlHelper {

        public static IDeserializer Deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        public static ISerializer Serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();

        public static IDeserializer DeserializerUsing(object objectToBind) {
            IObjectFactory defaultObjectFactory = new DefaultObjectFactory();
            Type objectType = objectToBind.GetType();

            return new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithObjectFactory(type => type == objectType ? objectToBind : defaultObjectFactory.Create(type))
                .Build();
        }

    }
}
