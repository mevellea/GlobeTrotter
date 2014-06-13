using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace DropboxsService.Common.JSON
{
    [DataContract]
    public class ResponseContainer
    {
        [DataMember(Name = "size", EmitDefaultValue = false)]
        public string size { get; set; }

        [DataMember(Name = "hash", EmitDefaultValue = false)]
        public string hash { get; set; }

        [DataMember(Name = "bytes", EmitDefaultValue = false)]
        public int bytes { get; set; } 

        [DataMember(Name = "thumb_exists", EmitDefaultValue = false)]
        public bool thumb_exists { get; set; }

        [DataMember(Name = "rev", EmitDefaultValue = false)]
        public string rev { get; set; }

        [DataMember(Name = "modified", EmitDefaultValue = false)]
        public string modified { get; set; }

        [DataMember(Name = "path", EmitDefaultValue = false)]
        public string path { get; set; }

        [DataMember(Name = "is_dir", EmitDefaultValue = false)]
        public bool is_dir { get; set; }

        [DataMember(Name = "icon", EmitDefaultValue = false)]
        public string icon { get; set; }

        [DataMember(Name = "root", EmitDefaultValue = false)]
        public string root { get; set; }

        [DataMember(Name = "mime_type", EmitDefaultValue = false)]
        public string mime_type { get; set; }

        [DataMember(Name = "revision", EmitDefaultValue = false)]
        public int revision { get; set; }

        [DataMember(Name = "contents", EmitDefaultValue = false)]
        public ResponseElement[] contents { get; set; }
    }

    [DataContract]
    public class ResponseToken
    {
        [DataMember(Name = "access_token", EmitDefaultValue = false)]
        public string access_token { get; set; }

        [DataMember(Name = "token_type", EmitDefaultValue = false)]
        public string token_type { get; set; }

        [DataMember(Name = "uid", EmitDefaultValue = false)]
        public int uid { get; set; }
    }

    [DataContract]
    public class ResponseElement
    {
        [DataMember(Name = "size", EmitDefaultValue = false)]
        public string size { get; set; }

        [DataMember(Name = "rev", EmitDefaultValue = false)]
        public string rev { get; set; }

        [DataMember(Name = "thumb_exists", EmitDefaultValue = false)]
        public bool thumb_exists { get; set; }

        [DataMember(Name = "bytes", EmitDefaultValue = false)]
        public int bytes { get; set; }

        [DataMember(Name = "modified", EmitDefaultValue = false)]
        public string modified { get; set; }

        [DataMember(Name = "path", EmitDefaultValue = false)]
        public string path { get; set; }

        [DataMember(Name = "is_dir", EmitDefaultValue = false)]
        public bool is_dir { get; set; }

        [DataMember(Name = "icon", EmitDefaultValue = false)]
        public string icon { get; set; }

        [DataMember(Name = "root", EmitDefaultValue = false)]
        public string root { get; set; }

        [DataMember(Name = "mime_type", EmitDefaultValue = false)]
        public string mime_type { get; set; }

        [DataMember(Name = "revision", EmitDefaultValue = false)]
        public int revision { get; set; }
    }

    [DataContract]
    public class ResponseAccount
    {
        [DataMember(Name = "referral_link", EmitDefaultValue = false)]
        public string referral_link { get; set; }

        [DataMember(Name = "display_name", EmitDefaultValue = false)]
        public string display_name { get; set; }

        [DataMember(Name = "uid", EmitDefaultValue = false)]
        public int uid { get; set; }

        [DataMember(Name = "country", EmitDefaultValue = false)]
        public string country { get; set; }

        //[DataMember(Name = "QuotaInfo", EmitDefaultValue = false)]
        //public QuotaInfo QuotaInfo { get; set; }
    }

    [DataContract]
    public class Chunk
    {
        [DataMember(Name = "upload_id", EmitDefaultValue = false)]
        public string upload_id { get; set; }

        [DataMember(Name = "offset", EmitDefaultValue = false)]
        public int offset { get; set; }

        [DataMember(Name = "expires", EmitDefaultValue = false)]
        public string expires { get; set; }
    }
}