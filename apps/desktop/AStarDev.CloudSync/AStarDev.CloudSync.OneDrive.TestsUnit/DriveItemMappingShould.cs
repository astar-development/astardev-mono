using System.Text.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class DriveItemMappingShould
{
    [Fact]
    public void map_files_and_folders_to_provider_items()
    {
        const string json = """
            {
              "value": [
                { "id": "f1", "name": "a.txt", "size": 12, "lastModifiedDateTime": "2026-01-02T03:04:05Z", "file": {}, "parentReference": { "path": "/drive/root:" } },
                { "id": "d1", "name": "Docs", "size": 99, "lastModifiedDateTime": "2026-02-03T04:05:06Z", "folder": { "childCount": 2 }, "parentReference": { "path": "/drive/root:" } },
                { "id": "f2", "name": "b.txt", "size": 5, "lastModifiedDateTime": "2026-03-04T05:06:07Z", "file": {}, "parentReference": { "path": "/drive/root:/Docs" } }
              ]
            }
            """;
        var page = JsonSerializer.Deserialize<GraphChildrenPage>(json, GraphJson.Options)!;

        var items = page.Value!.Select(DriveItemMapping.ToProviderItem).ToList();

        items.ShouldBe(
        [
            new ProviderItem(new RemoteItemId("f1"), new RelativePath("a.txt"), ItemType.File, 12, DateTimeOffset.Parse("2026-01-02T03:04:05Z")),
            new ProviderItem(new RemoteItemId("d1"), new RelativePath("Docs"), ItemType.Folder, 99, DateTimeOffset.Parse("2026-02-03T04:05:06Z")),
            new ProviderItem(new RemoteItemId("f2"), new RelativePath("Docs/b.txt"), ItemType.File, 5, DateTimeOffset.Parse("2026-03-04T05:06:07Z"))
        ]);
    }
}
