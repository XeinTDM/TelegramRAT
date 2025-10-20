using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace TelegramRAT.Features;

[Serializable, XmlRoot(ElementName = "query")]
public class NetworkInfo
{
    [XmlElement(ElementName = "status")]
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [XmlElement(ElementName = "country")]
    [JsonPropertyName("country")]
    public string Country { get; set; }

    [XmlElement(ElementName = "countryCode")]
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; }

    [XmlElement(ElementName = "region")]
    [JsonPropertyName("region")]
    public string Region { get; set; }

    [XmlElement(ElementName = "regionName")]
    [JsonPropertyName("regionName")]
    public string RegionName { get; set; }

    [XmlElement(ElementName = "city")]
    [JsonPropertyName("city")]
    public string City { get; set; }

    [XmlElement(ElementName = "zip")]
    [JsonPropertyName("zip")]
    public string Zip { get; set; }

    [XmlElement(ElementName = "lat")]
    [JsonPropertyName("lat")]
    public string Lat { get; set; }

    [XmlElement(ElementName = "lon")]
    [JsonPropertyName("lon")]
    public string Lon { get; set; }

    [XmlElement(ElementName = "timezone")]
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    [XmlElement(ElementName = "isp")]
    [JsonPropertyName("isp")]
    public string Isp { get; set; }

    [XmlElement(ElementName = "org")]
    [JsonPropertyName("org")]
    public string Org { get; set; }

    [XmlElement(ElementName = "as")]
    [JsonPropertyName("as")]
    public string As { get; set; }

    [XmlElement(ElementName = "query")]
    [JsonPropertyName("query")]
    public string Query { get; set; }
}
