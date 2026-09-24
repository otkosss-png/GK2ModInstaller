using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Steamworks;

internal static class Program
{
    private const uint AppId = 4358690;

    private static int Main(string[] args)
    {
        string mode = "create", folder = null, title = null, desc = null, descFile = null, tags = null, preview = null;
        ulong updateId = 0, deleteId = 0;
        bool pub = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--update": updateId = ulong.Parse(args[++i]); mode = "update"; break;
                case "--delete": deleteId = ulong.Parse(args[++i]); mode = "delete"; break;
                case "--folder": folder = args[++i]; break;
                case "--title": title = args[++i]; break;
                case "--desc": desc = args[++i]; break;
                case "--desc-file": descFile = args[++i]; break;
                case "--tags": tags = args[++i]; break;
                case "--preview": preview = args[++i]; break;
                case "--public": pub = true; break;
            }
        }
        if (descFile != null && File.Exists(descFile)) desc = File.ReadAllText(descFile);

        if (!SteamAPI.Init())
        {
            Console.WriteLine("SteamAPI.Init() FAILED");
            return 3;
        }
        Console.WriteLine("Steam user: " + SteamFriends.GetPersonaName());

        if (mode == "delete") return DoDelete(deleteId);
        if (mode == "update") return DoUpdate(updateId, title, desc, tags, preview, pub);
        return DoCreate(folder, title, desc, tags, pub);
    }

    private static int DoDelete(ulong id)
    {
        var res = CallResult<DeleteItemResult_t>.Create();
        DeleteItemResult_t r = default; bool done = false;
        res.Set(SteamUGC.DeleteItem(new PublishedFileId_t(id)), (x, io) => { r = x; done = true; });
        while (!done) { SteamAPI.RunCallbacks(); Thread.Sleep(50); }
        Console.WriteLine("DeleteItem " + id + ": " + r.m_eResult);
        SteamAPI.Shutdown();
        return r.m_eResult == EResult.k_EResultOK ? 0 : 5;
    }

    private static int DoCreate(string folder, string title, string desc, string tags, bool pub)
    {
        if (folder == null || title == null) { Console.WriteLine("create needs --folder and --title"); return 2; }

        var createRes = CallResult<CreateItemResult_t>.Create();
        CreateItemResult_t created = default; bool createDone = false;
        createRes.Set(SteamUGC.CreateItem(new AppId_t(AppId), EWorkshopFileType.k_EWorkshopFileTypeCommunity),
            (r, io) => { created = r; createDone = true; });
        while (!createDone) { SteamAPI.RunCallbacks(); Thread.Sleep(50); }
        if (created.m_eResult != EResult.k_EResultOK) { Console.WriteLine("CreateItem failed: " + created.m_eResult); return 4; }
        var id = created.m_nPublishedFileId;
        Console.WriteLine("created item " + id.m_PublishedFileId);

        var handle = SteamUGC.StartItemUpdate(new AppId_t(AppId), id);
        SteamUGC.SetItemTitle(handle, title);
        if (desc != null) SteamUGC.SetItemDescription(handle, desc);
        bool contentOk = SteamUGC.SetItemContent(handle, Path.GetFullPath(folder));
        Console.WriteLine("SetItemContent=" + contentOk);
        SteamUGC.SetItemVisibility(handle, pub
            ? ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic
            : ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted);
        if (!string.IsNullOrEmpty(tags)) SteamUGC.SetItemTags(handle, new List<string>(tags.Split(',')));
        return Submit(handle, id.m_PublishedFileId);
    }

    private static int DoUpdate(ulong id, string title, string desc, string tags, string preview, bool pub)
    {
        var handle = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(id));
        if (title != null) SteamUGC.SetItemTitle(handle, title);
        if (desc != null) SteamUGC.SetItemDescription(handle, desc);
        if (!string.IsNullOrEmpty(tags)) SteamUGC.SetItemTags(handle, new List<string>(tags.Split(',')));
        if (preview != null) Console.WriteLine("SetItemPreview=" + SteamUGC.SetItemPreview(handle, Path.GetFullPath(preview)));
        if (pub) SteamUGC.SetItemVisibility(handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
        Console.WriteLine("updating item " + id);
        return Submit(handle, id);
    }

    private static int Submit(UGCUpdateHandle_t handle, ulong id)
    {
        var subRes = CallResult<SubmitItemUpdateResult_t>.Create();
        SubmitItemUpdateResult_t sub = default; bool done = false;
        subRes.Set(SteamUGC.SubmitItemUpdate(handle, "Update"), (r, io) => { sub = r; done = true; });
        while (!done) { SteamAPI.RunCallbacks(); Thread.Sleep(50); }
        Console.WriteLine("SubmitItemUpdate: " + sub.m_eResult + " item " + id);
        SteamAPI.Shutdown();
        return sub.m_eResult == EResult.k_EResultOK ? 0 : 5;
    }
}
