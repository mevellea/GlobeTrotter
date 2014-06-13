using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Xml.Serialization;
using System.Diagnostics;

namespace GlobeTrotter
{
    class Serialization
    {
        public static async Task<Boolean> SerializeBinary<T>(string fileName, T _data)
        {
            try
            {
                MemoryStream sessionData = new MemoryStream();
                DataContractSerializer serializer = new DataContractSerializer(typeof(T));
                serializer.WriteObject(sessionData, _data);

                // Get an output stream for the SessionState file and write the state asynchronously
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    sessionData.Seek(0, SeekOrigin.Begin);
                    await sessionData.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return true;
        }

        public static async Task<Boolean> SerializeToXmlFile<T>(string fileName, T serializationSource)
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    fileName, CreationCollisionOption.ReplaceExisting);

                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(T));

                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.NewLineHandling = NewLineHandling.Entitize;
                    XmlWriter writer = XmlWriter.Create(stream.AsStreamForWrite(), settings);
                    var ser = new XmlSerializer(typeof(T));
                    ser.Serialize(writer, serializationSource);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }

        public static T DeserializeFromXmlFile<T>(string fileName)
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    return (T)ser.Deserialize(reader);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return default(T);
            }
        }

        public static async Task<object> DeserializeHttpToJson<T>(HttpContent Content)
        {
            Stream inputStream = await Content.ReadAsStreamAsync();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            inputStream.Seek(0, SeekOrigin.Begin);
            return (T)ser.ReadObject(inputStream);
        }

        public static async Task<object> DeserializeHttpToXml<T>(HttpContent Content, String rootName, String rootNamespace)
        {
            Stream inputStream = await Content.ReadAsStreamAsync();
            DataContractSerializer ser = new DataContractSerializer(typeof(T), rootName, rootNamespace);
            inputStream.Seek(0, SeekOrigin.Begin);
            return (T)ser.ReadObject(inputStream);
        }
    }
}
