namespace AStarDev.CloudSync.IPC;

/// <summary>Which stage of a folder's sync a <see cref="CrawlProgressChanged"/> report is about.</summary>
public enum CrawlPhase
{
    /// <summary>Listing the folder for the first time; <see cref="CrawlProgressChanged.ItemsIndexed"/> counts items stored.</summary>
    Crawling,

    /// <summary>Applying changes to an already listed folder, after the first listing.</summary>
    Syncing
}
