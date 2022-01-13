﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace GHXR
{
    class ShareableParameterConverter : JsonCreationConverter<ShareableParameter>
    {
        protected override ShareableParameter Create(Type objectType, JObject jObject)
        {
            switch (jObject["Type"].Value<string>())
            {
                case "toggle":
                    return new ShareableParameter.ShareableToggle();
                case "slider":
                    return new ShareableParameter.ShareableSlider();
                case "list":
                    return new ShareableParameter.ShareableList();
                case "knob":
                    return new ShareableParameter.ShareableKnob();
                /*case "colour":
                    return new ShareableParameter.ShareableColour();*/
            }
            return null;
        }
    }

    public abstract class JsonCreationConverter<T> : JsonConverter
    {
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType)
        {
            return typeof(T) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            try
            {
                var jObject = JObject.Load(reader);
                var target = Create(objectType, jObject);
                serializer.Populate(jObject.CreateReader(), target);
                return target;
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
