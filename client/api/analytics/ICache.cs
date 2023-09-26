﻿using io.harness.cfsdk.client.dto;
using System.Collections.Generic;

namespace io.harness.cfsdk.client.api.analytics
{
    public interface ICache
    {
        int get(Analytics a);

        IDictionary<Analytics, int> getAll();

        void put(Analytics a, int i);

        void resetCache();

        void printCache();
    }
}
