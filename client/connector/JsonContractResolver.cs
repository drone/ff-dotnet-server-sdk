using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace io.harness.cfsdk.client.connector
{
    /// <summary>
    /// This ContractResolver will adjust some JSON properties to make them less strict.
    /// Currently client-v1.yaml does not require certain attributes however the code generator still adds
    /// Newtonsoft.Json.Required.DisallowNull which will throw an exception if the property is present but null.
    /// There does not seem to be any way to configuring the generator to remove the DisallowNull at code generation
    /// time, so instead we do it here dynamically setting each to Newtonsoft.Json.Required.Default.
    /// 
    /// https://github.com/RicoSuter/NSwag/issues/850
    /// https://www.newtonsoft.com/json/help/html/t_newtonsoft_json_required.htm
    /// </summary>
    class JsonContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            OverrideRequiredProperty(ref property);
            return property;
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            var contract = base.CreateObjectContract(objectType);
            contract.ItemRequired = Required.Default;
            return contract;
        }

        protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty matchingMemberProperty, ParameterInfo parameterInfo)
        {
            var property = base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo);
            OverrideRequiredProperty(ref property);
            return property;
        }

        private void OverrideRequiredProperty(ref JsonProperty property)
        {
            if (property.NullValueHandling != NullValueHandling.Ignore ||
                property.Required != Required.DisallowNull) return;
            Log.Debug($"Changing JSON property '{property.PropertyName}' from Required.DisallowNull to Required.Default");
            property.Required = Required.Default;
        }
        
        private void OverrideRequiredProperty(ref  JsonObjectContract contract)
        {
            if (contract.ItemNullValueHandling != NullValueHandling.Ignore ||
                contract.ItemRequired != Required.DisallowNull) return;
            Log.Debug($"Changing JSON object contract '{contract}' from Required.DisallowNull to Required.Default");
            contract.ItemRequired = Required.Default;
        }
    }
}