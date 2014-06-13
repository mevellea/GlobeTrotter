using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace GoogleService.Common.JSON
{
    [DataContract]
    public class ResponseToken
    {
        [DataMember(Name = "access_token", EmitDefaultValue = false)]
        public string access_token { get; set; }

        [DataMember(Name = "refresh_token", EmitDefaultValue = false)]
        public string refresh_token { get; set; }

        [DataMember(Name = "expires_in", EmitDefaultValue = false)]
        public int expires_in { get; set; }

        [DataMember(Name = "token_type", EmitDefaultValue = false)]
        public string token_type { get; set; }
    }

    [DataContract]
    public class ResponseFeed
    {
        [DataMember(Name = "id", EmitDefaultValue = false)]
        public string id { get; set; }

        [DataMember(Name = "updated", EmitDefaultValue = false)]
        public string updated { get; set; }
    }

    [DataContract]
    public class ResponsePlace
    {
        [DataMember(Name = "results", EmitDefaultValue = false)]
        public Results[] results { get; set; }
    }

    [DataContract]
    public class ResponseDirection
    {
        [DataMember(Name = "routes", EmitDefaultValue = false)]
        public GRoute[] routes { get; set; }

        [DataMember(Name = "status", EmitDefaultValue = false)]
        public string status { get; set; }
    }

    [DataContract]
    public class GRoute
    {
        [DataMember(Name = "legs", EmitDefaultValue = false)]
        public Leg[] legs { get; set; }
    }

    [DataContract]
    public class Leg
    {
        [DataMember(Name = "distance", EmitDefaultValue = false)]
        public Distance distance { get; set; }

        [DataMember(Name = "steps", EmitDefaultValue = false)]
        public Step[] steps { get; set; }
    }

    [DataContract]
    public class Step
    {
        [DataMember(Name = "distance", EmitDefaultValue = false)]
        public Distance distance { get; set; }

        [DataMember(Name = "polyline", EmitDefaultValue = false)]
        public Polyline polyline { get; set; }
    }

    [DataContract]
    public class Distance
    {
        [DataMember(Name = "text", EmitDefaultValue = false)]
        public string text { get; set; }

        [DataMember(Name = "value", EmitDefaultValue = false)]
        public int value { get; set; }
    }

    [DataContract]
    public class Polyline
    {
        [DataMember(Name = "points", EmitDefaultValue = false)]
        public string points { get; set; }
    }

    [DataContract]
    public class ResponseAddress
    {
        [DataMember(Name = "result", EmitDefaultValue = false)]
        public Result result { get; set; }
    }

    [DataContract]
    public class Result
    {
        [DataMember(Name = "address_components", EmitDefaultValue = false)]
        public AddressComp[] address_components { get; set; }
    }

    [DataContract]
    public class AddressComp
    {
        [DataMember(Name = "long_name", EmitDefaultValue = false)]
        public string long_name { get; set; }

        [DataMember(Name = "short_name", EmitDefaultValue = false)]
        public string short_name { get; set; }

        [DataMember(Name = "types", EmitDefaultValue = false)]
        public string[] types { get; set; }
    }

    [DataContract]
    public class Results
    {
        [DataMember(Name = "reference", EmitDefaultValue = false)]
        public string reference { get; set; }
    }
}