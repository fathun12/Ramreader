using System.Collections;
using Ramreader.Core.Memory;
using Ramreader.Core.Models;

namespace Ramreader.Core.Scanning;

/// <summary>
/// Scanner nilai bergaya Cheat Engine: scan pertama (nilai tepat, snapshot tanpa nilai awal,
/// atau rentang nilai) lalu scan lanjutan untuk menyaring kandidat. Dijalankan dari thread latar.
/// </summary>
public sealed class ValueScanner
{
    /// <summary>Batas jumlah kandidat supaya RAM aplikasi ini tidak membengkak.</summary>
    public const int MaxHits = 5_000_000;

    /// <summary>Batas total byte snapshot yang disimpan di RAM (snapshot dan scan rentang).</summary>
    public const long MaxSnapshotBytes = 3L * 1024 * 1024 * 1024;

    private const ulong ReadWindow = 1UL << 20; // 1 MB per pembacaan batch saat scan lanjutan

    /// <summary>Satu potongan snapshot: isi memori terakhir + penanda elemen mana yang masih kandidat.</summary>
    private sealed record SnapshotBlock(ulong Address, byte[] Data, BitArray Alive);

    private List<ScanHit> _hits = new();
    private Queue<SnapshotBlock>? _snapshot;
    private int _snapshotCount;

    public ScanValueType ValueType { get; private set; } = ScanValueType.Int32;
    public bool HasScan { get; private set; }

    /// <summary>True kalau hasil dipotong karena melewati MaxHits (alamat target mungkin tidak ikut).</summary>
    public bool Truncated { get; private set; }

    /// <summary>True kalau snapshot hanya memuat sebagian memori game (melewati MaxSnapshotBytes).</summary>
    public bool SnapshotTruncated { get; private set; }

    /// <summary>True selama kandidat masih terlalu banyak untuk berupa daftar alamat (mode snapshot padat).</summary>
    public bool IsSnapshot => _snapshot is not null;

    /// <summary>Daftar alamat kandidat. Kosong selama masih mode snapshot.</summary>
    public IReadOnlyList<ScanHit> Hits => _hits;

    public int Count => IsSnapshot ? _snapshotCount : _hits.Count;

    public void Reset()
    {
        _hits = new List<ScanHit>();
        _snapshot = null;
        _snapshotCount = 0;
        HasScan = false;
        Truncated = false;
        SnapshotTruncated = false;
    }

    /// <summary>Scan pertama: cari semua alamat writable yang berisi nilai persis sama.</summary>
    public void FirstScan(ProcessMemory memory, ScanValueType type, ulong target,
        IProgress<double>? progress, CancellationToken ct)
    {
        int size = ValueCodec.SizeOf(type);
        var hits = new List<ScanHit>();
        bool truncated = false;

        double total = memory.EnumerateRegions(true).Sum(r => (double)r.Size);
        if (total <= 0) total = 1;
        double done = 0;

        foreach (var (address, data) in memory.ReadChunks(true))
        {
            ct.ThrowIfCancellationRequested();

            for (int i = 0; i + size <= data.Length; i += size)
            {
                ulong raw = size == 4 ? BitConverter.ToUInt32(data, i) : BitConverter.ToUInt64(data, i);
                if (raw != target) continue;

                if (hits.Count >= MaxHits)
                {
                    truncated = true;
                    break;
                }
                hits.Add(new ScanHit(address + (ulong)i, raw, raw));
            }

            done += data.Length;
            progress?.Report(done / total);
            if (truncated) break;
        }

        _hits = hits;
        _snapshot = null;
        _snapshotCount = 0;
        ValueType = type;
        HasScan = true;
        Truncated = truncated;
        SnapshotTruncated = false;
        progress?.Report(1);
    }

    /// <summary>
    /// Scan pertama tanpa nilai awal: simpan isi seluruh memori writable (snapshot).
    /// Setelah itu disaring lewat NextScan (berubah / tidak berubah / bertambah / berkurang / rentang).
    /// </summary>
    public void FirstScanUnknown(ProcessMemory memory, ScanValueType type,
        IProgress<double>? progress, CancellationToken ct)
        => CaptureSnapshot(memory, type, false, 0, 0, progress, ct);

    /// <summary>
    /// Scan pertama dengan rentang nilai: kandidat = semua alamat writable yang nilainya
    /// di antara min dan max (inklusif). Kalau min > max, keduanya ditukar.
    /// </summary>
    public void FirstScanRange(ProcessMemory memory, ScanValueType type, ulong min, ulong max,
        IProgress<double>? progress, CancellationToken ct)
    {
        if (ValueCodec.Compare(type, min, max) > 0) (min, max) = (max, min);
        CaptureSnapshot(memory, type, true, min, max, progress, ct);
    }

    /// <summary>
    /// Dasar snapshot dan scan rentang. Blok memori yang tidak punya satu pun kandidat tidak disimpan.
    /// Kalau kandidat akhirnya &lt;= MaxHits, langsung diubah jadi daftar alamat biasa.
    /// </summary>
    private void CaptureSnapshot(ProcessMemory memory, ScanValueType type, bool filter,
        ulong min, ulong max, IProgress<double>? progress, CancellationToken ct)
    {
        // Buang hasil lama dulu supaya RAM tidak terpakai dobel.
        Reset();

        int size = ValueCodec.SizeOf(type);
        var blocks = new Queue<SnapshotBlock>();
        long bytes = 0;
        long alive = 0;
        bool truncated = false;

        double total = memory.EnumerateRegions(true).Sum(r => (double)r.Size);
        if (total <= 0) total = 1;
        double done = 0;

        foreach (var (address, data) in memory.ReadChunks(true))
        {
            ct.ThrowIfCancellationRequested();

            int count = data.Length / size;
            if (count > 0)
            {
                BitArray flags;
                int blockAlive;

                if (filter)
                {
                    flags = new BitArray(count, false);
                    blockAlive = 0;
                    for (int e = 0; e < count; e++)
                    {
                        ulong raw = ValueCodec.ReadRaw(data, e * size, size);
                        if (!ValueCodec.InRange(type, raw, min, max)) continue;
                        flags[e] = true;
                        blockAlive++;
                    }
                }
                else
                {
                    flags = new BitArray(count, true);
                    blockAlive = count;
                }

                if (blockAlive > 0)
                {
                    if (bytes + data.Length > MaxSnapshotBytes)
                    {
                        truncated = true;
                        break;
                    }

                    blocks.Enqueue(new SnapshotBlock(address, data, flags));
                    bytes += data.Length;
                    alive += blockAlive;
                }
            }

            done += data.Length;
            progress?.Report(done / total);
        }

        ValueType = type;
        HasScan = true;
        SnapshotTruncated = truncated;

        if (alive <= MaxHits)
        {
            _hits = BlocksToHits(blocks, size);
            _snapshot = null;
            _snapshotCount = 0;
        }
        else
        {
            _hits = new List<ScanHit>();
            _snapshot = blocks;
            _snapshotCount = (int)alive;
        }

        progress?.Report(1);
    }

    private static List<ScanHit> BlocksToHits(Queue<SnapshotBlock> blocks, int size)
    {
        var hits = new List<ScanHit>();
        foreach (var block in blocks)
        {
            int count = block.Data.Length / size;
            for (int e = 0; e < count; e++)
            {
                if (!block.Alive[e]) continue;
                ulong raw = ValueCodec.ReadRaw(block.Data, e * size, size);
                hits.Add(new ScanHit(block.Address + (ulong)(e * size), raw, raw));
            }
        }
        return hits;
    }

    /// <summary>
    /// Scan lanjutan: baca ulang tiap kandidat, simpan yang memenuhi mode.
    /// Untuk mode Between, target = batas bawah dan upper = batas atas.
    /// </summary>
    public void NextScan(ProcessMemory memory, ScanMode mode, ulong target,
        IProgress<double>? progress, CancellationToken ct, ulong upper = 0)
    {
        if (!HasScan) throw new InvalidOperationException("Belum ada scan pertama.");

        if (mode == ScanMode.Between && ValueCodec.Compare(ValueType, target, upper) > 0)
            (target, upper) = (upper, target);

        if (_snapshot is not null)
        {
            NextScanSnapshot(memory, mode, target, upper, progress, ct);
            return;
        }

        int size = ValueCodec.SizeOf(ValueType);
        var survivors = new List<ScanHit>();
        int n = _hits.Count;
        int i = 0;

        while (i < n)
        {
            ct.ThrowIfCancellationRequested();

            // Kandidat berurutan menurut alamat, jadi dibaca per blok supaya tidak
            // memanggil ReadProcessMemory jutaan kali.
            ulong start = _hits[i].Address;
            int j = i + 1;
            while (j < n && _hits[j].Address + (ulong)size - start <= ReadWindow) j++;

            int length = (int)(_hits[j - 1].Address + (ulong)size - start);
            byte[]? block = memory.Read(start, length);

            for (int k = i; k < j; k++)
            {
                var hit = _hits[k];
                ulong current;

                if (block is not null)
                {
                    current = ValueCodec.ReadRaw(block, (int)(hit.Address - start), size);
                }
                else
                {
                    // Ada halaman yang tidak terbaca di dalam blok: coba satu per satu.
                    var single = memory.Read(hit.Address, size);
                    if (single is null) continue;
                    current = ValueCodec.ReadRaw(single, 0, size);
                }

                if (Matches(mode, hit.Value, current, target, upper))
                    survivors.Add(new ScanHit(hit.Address, current, hit.Value));
            }

            i = j;
            progress?.Report((double)i / n);
        }

        _hits = survivors;
        progress?.Report(1);
    }

    /// <summary>
    /// Scan lanjutan untuk mode snapshot padat. Blok lama dibuang satu per satu supaya RAM tidak dobel,
    /// jadi kalau scan dibatalkan di tengah jalan snapshot ikut dibuang (Reset).
    /// </summary>
    private void NextScanSnapshot(ProcessMemory memory, ScanMode mode, ulong target, ulong upper,
        IProgress<double>? progress, CancellationToken ct)
    {
        var pending = _snapshot!;
        int size = ValueCodec.SizeOf(ValueType);
        var kept = new Queue<SnapshotBlock>();
        var hits = new List<ScanHit>();
        long alive = 0;

        double total = pending.Sum(b => (double)b.Data.Length);
        if (total <= 0) total = 1;
        double done = 0;

        try
        {
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var block = pending.Dequeue();
                var current = memory.Read(block.Address, block.Data.Length);

                // Blok yang tidak terbaca lagi (memori sudah dibebaskan game) dibuang.
                if (current is not null)
                {
                    int count = block.Data.Length / size;
                    var stillAlive = new BitArray(count, false);
                    int blockAlive = 0;

                    for (int e = 0; e < count; e++)
                    {
                        if (!block.Alive[e]) continue;

                        int offset = e * size;
                        ulong before = ValueCodec.ReadRaw(block.Data, offset, size);
                        ulong now = ValueCodec.ReadRaw(current, offset, size);
                        if (!Matches(mode, before, now, target, upper)) continue;

                        stillAlive[e] = true;
                        blockAlive++;

                        // Daftar alamat hanya dibangun sampai batas; kalau lewat, tetap dihitung tapi tidak disimpan.
                        if (hits.Count <= MaxHits)
                            hits.Add(new ScanHit(block.Address + (ulong)offset, now, before));
                    }

                    if (blockAlive > 0)
                        kept.Enqueue(new SnapshotBlock(block.Address, current, stillAlive));
                    alive += blockAlive;
                }

                done += block.Data.Length;
                progress?.Report(done / total);
            }
        }
        catch
        {
            // Blok lama sudah terbuang sebagian, jadi keadaan tidak bisa dipulihkan.
            Reset();
            throw;
        }

        if (alive <= MaxHits)
        {
            // Sudah cukup sedikit: ganti ke daftar alamat biasa.
            _hits = hits;
            _snapshot = null;
            _snapshotCount = 0;
        }
        else
        {
            _hits = new List<ScanHit>();
            _snapshot = kept;
            _snapshotCount = (int)alive;
        }

        progress?.Report(1);
    }

    /// <summary>Baca nilai terkini satu alamat (untuk tampilan langsung di daftar).</summary>
    public ulong? ReadCurrent(ProcessMemory memory, ulong address)
    {
        int size = ValueCodec.SizeOf(ValueType);
        var bytes = memory.Read(address, size);
        return bytes is null ? null : ValueCodec.ReadRaw(bytes, 0, size);
    }

    private bool Matches(ScanMode mode, ulong old, ulong current, ulong target, ulong upper) => mode switch
    {
        ScanMode.Exact => current == target,
        ScanMode.Changed => current != old,
        ScanMode.Unchanged => current == old,
        ScanMode.Increased => ValueCodec.Compare(ValueType, current, old) > 0,
        ScanMode.Decreased => ValueCodec.Compare(ValueType, current, old) < 0,
        ScanMode.Between => ValueCodec.InRange(ValueType, current, target, upper),
        _ => false
    };
}