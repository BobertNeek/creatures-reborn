using System;
using System.Collections.Generic;
using Godot;

namespace CreaturesReborn.Godot.BrainGpu;

internal sealed class BrainGpuStorageBufferCache : IDisposable
{
    private sealed record Slot(Rid Buffer, int Size);

    private readonly RenderingDevice _device;
    private readonly Dictionary<string, Slot> _slots = new();
    private bool _disposed;

    public BrainGpuStorageBufferCache(RenderingDevice device)
    {
        _device = device;
    }

    public int BufferRebuildsThisTick { get; private set; }

    public void ResetTickMetrics()
        => BufferRebuildsThisTick = 0;

    public Rid Upload(string key, byte[] bytes)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BrainGpuStorageBufferCache));

        if (bytes.Length == 0)
            throw new ArgumentException("RenderingDevice storage buffers must not be empty.", nameof(bytes));

        if (_slots.TryGetValue(key, out Slot? slot) && slot.Size == bytes.Length)
        {
            Error updated = _device.BufferUpdate(slot.Buffer, 0, (uint)bytes.Length, bytes);
            if (updated != Error.Ok)
                throw new InvalidOperationException($"RenderingDevice failed to update storage buffer '{key}' ({updated}).");
            return slot.Buffer;
        }

        if (slot != null && slot.Buffer.IsValid)
            _device.FreeRid(slot.Buffer);

        RenderingDevice.StorageBufferUsage storageUsage = (RenderingDevice.StorageBufferUsage)0;
        Rid buffer = _device.StorageBufferCreate((uint)bytes.Length, bytes, storageUsage);
        if (!buffer.IsValid)
            throw new InvalidOperationException($"RenderingDevice failed to create storage buffer '{key}'.");

        _slots[key] = new Slot(buffer, bytes.Length);
        BufferRebuildsThisTick++;
        return buffer;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (Slot slot in _slots.Values)
        {
            if (slot.Buffer.IsValid)
                _device.FreeRid(slot.Buffer);
        }

        _slots.Clear();
        _disposed = true;
    }
}
