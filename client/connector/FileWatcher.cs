﻿using System;
using System.IO;
using io.harness.cfsdk.client.api;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.connector
{
    internal sealed class FileWatcher : IService
    {
        private readonly string domain;
        private readonly string path;
        private readonly IUpdateCallback callback;
        private readonly ILogger logger;

        private FileSystemWatcher watcher;

        public FileWatcher(string domain, string path, IUpdateCallback callback, ILogger logger = null)
        {
            this.domain = domain;
            this.path = path;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            this.logger = logger ?? Config.DefaultLogger;
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "delete", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "create", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            callback.Update(new Message() { Domain = domain, Event = "patch", Identifier = Path.GetFileNameWithoutExtension(e.Name), Version = 0 }, false);
        }

        public void Close()
        {
            Stop();
        }

        public void Start()
        {
            Stop();

            try
            {
                watcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = "*.json",
                    EnableRaisingEvents = true
                };
                watcher.Changed += Watcher_Changed;
                watcher.Created += Watcher_Created;
                watcher.Deleted += Watcher_Deleted;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating fileWatcher at {Path}", path);
                Stop();
            }
        }

        public void Stop()
        {
            if (watcher != null)
            {
                watcher.Dispose();
                watcher = null;
            }
        }
    }
}
