using Meducate.Infrastructure.Icd11;

namespace Meducate.Tests;

public class Icd11ApiClientTests
{
    [Fact]
    public void ParseSearchResponse_ReturnsFirstCodeableEntity()
    {
        var json = """
        {
          "destinationEntities": [
            { "id": "http://id.who.int/icd/entity/1", "title": "<em class='found'>Asthma</em>", "theCode": "CA23" },
            { "id": "http://id.who.int/icd/entity/2", "title": "Other Match", "theCode": "CA24" }
          ]
        }
        """;

        var match = Icd11ApiClient.ParseSearchResponse(json);

        Assert.NotNull(match);
        Assert.Equal("CA23", match!.Code);
        Assert.Equal("Asthma", match.Title);
    }

    [Fact]
    public void ParseSearchResponse_SkipsChaptersAndBlocksWithoutALeafCode()
    {
        var json = """
        {
          "destinationEntities": [
            { "id": "http://id.who.int/icd/entity/1", "title": "Certain infectious or parasitic diseases" },
            { "id": "http://id.who.int/icd/entity/2", "title": "Cholera", "theCode": "1A00" }
          ]
        }
        """;

        var match = Icd11ApiClient.ParseSearchResponse(json);

        Assert.NotNull(match);
        Assert.Equal("1A00", match!.Code);
    }

    [Fact]
    public void ParseSearchResponse_ReturnsNull_WhenNoEntitiesHaveACode()
    {
        var json = """
        {
          "destinationEntities": [
            { "id": "http://id.who.int/icd/entity/1", "title": "Certain infectious or parasitic diseases" }
          ]
        }
        """;

        Assert.Null(Icd11ApiClient.ParseSearchResponse(json));
    }

    [Fact]
    public void ParseSearchResponse_ReturnsNull_WhenDestinationEntitiesIsEmpty()
    {
        Assert.Null(Icd11ApiClient.ParseSearchResponse("""{ "destinationEntities": [] }"""));
    }

    [Fact]
    public void ParseSearchResponse_ReturnsNull_WhenDestinationEntitiesIsMissing()
    {
        Assert.Null(Icd11ApiClient.ParseSearchResponse("""{ "words": [] }"""));
    }
}
