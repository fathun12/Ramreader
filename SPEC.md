# SPEC: Ramreader

> Dokumen serah-terima untuk sesi AI berikutnya. Baca SELURUHNYA sebelum menulis apa pun.
> Terakhir diperbarui: 1 Nov 2026. Status: Langkah 1-6 dan 8 (Simpan Alamat + kartu Tersimpan) terbukti jalan di NSUNSC, termasuk tema terpusat (Themes\AppStyles.xaml). Ekspor Profil terbukti (JSON NSUNSC.profile.json valid). Langkah 5b dibangun (snapshot terbukti; Scan Rentang belum dites). Urutan berikutnya: 7 (signature), backlog lain, trainer paling akhir. Lihat bagian 9.

## 0. Prompt pembuka untuk sesi baru

Tempel ini bersama isi dokumen ini:

"Ini spec proyek Ramreader. Baca semua, terutama bagian 2 (cara kerja denganku) dan bagian 9 (status). Lanjutkan dari langkah pertama yang belum selesai di tabel bagian 9. Jangan membangun apa pun sebelum aku minta. Ulangi dulu aturan kerja versimu supaya aku tahu kamu paham."

## 1. Tujuan proyek

### 1.1 Masalah yang diselesaikan
User membuat trainer game (single-player) dan kesulitannya selalu: **menemukan alamat/ID memori yang stabil**. Alamat berubah tiap game dijalankan (ASLR, alokasi dinamis di heap) dan sering patah saat game di-update. Cara biasa (Cheat Engine) menuntut keluar-masuk game berkali-kali.

### 1.2 Produk fase 1: pembaca RAM (Ramreader)
Aplikasi desktop Windows yang:
- menyambung ke proses game dan membaca memorinya (read-only),
- membantu menemukan alamat stabil dengan **sesedikit mungkin restart game**,
- menyimpan hasilnya per game supaya bisa dipakai trainer.

### 1.3 Goals fase 1
1. UI bersih, modern (Fluent/Windows 11, dark, Mica), UX terpandu: user tidak perlu paham istilah teknis untuk menyelesaikan satu alur.
2. Alur utama terpandu: pilih game -> cari nilai -> cari jalur stabil -> restart game -> verifikasi -> simpan.
3. Sesi tersimpan di disk (tutup app/game, progres tidak hilang).
4. Core (logika) terpisah dari UI supaya dipakai ulang oleh trainer.

### 1.4 Non-goals fase 1
- Tidak menulis ke memori game (read-only). Menulis baru di fase 2.
- Tidak melawan/mem-bypass anti-cheat. Tidak untuk game online/kompetitif.

### 1.5 Fase 2: trainer (setelah pembaca RAM stabil)
Aplikasi/proyek terpisah (`Ramreader.Trainer`) yang memakai `Ramreader.Core`:
- Mendapat izin `PROCESS_VM_WRITE | PROCESS_VM_OPERATION` (hanya trainer, bukan pembaca).
- Memuat "profil game" (JSON hasil ekspor Ramreader): daftar fitur, cara menemukan alamatnya, rentang nilai valid.
- Resolver alamat, urutan percobaan: (1) AOB signature -> pointer global (RIP-relative) -> offset, (2) pointer chain `Modul+offset`, (3) signature cadangan. Cache hasil sekali saat attach.
- Fitur: set nilai (uang/EXP/item), freeze nilai (timer menulis ulang), hotkey global, god mode via code patch (NOP) di tahap lanjut.
- Validasi sebelum menulis (mis. HP harus 0-100000) supaya tidak crash kalau pola salah.
- Versi game di profil, supaya update offset tidak perlu compile ulang.
- Game target contoh user: Naruto Ultimate Ninja Storm Connections, Stellar Blade (Unreal Engine), Monster Hunter Wilds (RE Engine, Denuvo, ada online), Normandy-style games, dll. Hanya mode offline/single-player.

## 2. Cara kerja dengan user (WAJIB DIPATUHI)

User berbahasa Indonesia, santai. Kalau ia menulis huruf kapital semua, itu tanda frustrasi karena AI melompat atau tidak mengikuti aturan.

1. **Desain dulu, bangun kalau diminta.** Jangan membuat/membangun apa pun saat user baru minta desain atau diskusi. Kalau ragu apakah sudah disuruh membangun, tanya.
2. **Kerja di root yang sudah ada:** `D:\Projects\Ramreader`. Jangan membuat folder proyek baru di luar itu. Scaffold "di tempat" (mis. `dotnet new ... -o src\X`).
3. **Pakai terminal untuk memulai/menambah proyek** (dotnet new, dotnet add package, dst).
4. **Banyak folder/file sekaligus:** beri satu perintah **PowerShell 5.1** untuk terminal VS Code di root proyek (hindari `&&`; pakai baris terpisah atau `;`). Jangan menyuruh user membuat satu per satu secara manual.
5. **Kode ditulis di chat. JANGAN membuat file** (tidak ada tool pembuatan file; user yang menyalin).
6. **Format perubahan kode:**
   - File baru/utuh: tulis seluruh isi, dengan path lengkap di judul.
   - Mengubah/menambah kode yang sudah ada: format **Sebelum / Sesudah** (blok "Sebelum" harus persis ada di file user).
7. **Beri label langkah bersambung** (4a, 4b, 4c...) supaya user bisa bilang "4c sudah, 4d gagal".
8. **Satu langkah besar per giliran**, diakhiri instruksi build/tes dan apa yang harus user laporkan. User akan menempel output terminal atau screenshot.
9. **Jujur soal kode yang belum diuji.** AI tidak bisa menjalankan kode user; sebutkan bagian yang paling mungkin bermasalah (terutama nama API WPF-UI).
10. **Maksimal satu pertanyaan per respons**, dan hanya kalau benar-benar menentukan arah.
11. Saat error: minta pesan error persis + isi file terkait, lalu perbaiki dengan format Sebelum/Sesudah. Jangan menebak berlebihan; kadang penyebabnya user salah menempel ke file (xaml vs xaml.cs tertukar pernah terjadi).
12. UI harus bagus dan UX bersih; keputusan teknologi diserahkan ke AI selama ramah user.
13. Aturan Sebelum/Sesudah berlaku juga untuk SPEC.md dan file non-kode. Jangan pernah menulis "ganti bagian ini dengan ini" atau "tambahkan di akhir" tanpa teks Sebelum yang persis ada di file. Edit kecil saja; jangan menyuruh user memindah blok besar, lebih baik ganti penomoran.

14. **Meminta file atau menyuruh menjalankan perintah:** jelaskan dulu masalahnya dan alasannya, lalu beri satu perintah PowerShell yang menyalin isi file ke clipboard (`Set-Clipboard`). Jangan membuat file di Desktop atau di mana pun, dan kumpulkan semua yang dibutuhkan dalam satu pesan. Jangan memakai istilah samar ("memblokir", "kutahan").

## 3. Lingkungan user

- Windows 11, VS Code, terminal **PowerShell 5.1**, root `D:\Projects\Ramreader`.
- .NET SDK **10.0.301** (jadi target framework `net10.0` / `net10.0-windows`).
- NuGet: `WPF-UI 4.3.0`, `CommunityToolkit.Mvvm 8.4.2`, `Microsoft.Extensions.DependencyInjection`.
- Jalankan: `dotnet build` lalu `dotnet run --project src\Ramreader.App`. Untuk membaca game terproteksi perlu terminal/VS Code **sebagai Administrator**.

## 4. Stack dan keputusan arsitektur

- **WPF + C#**, MVVM dengan CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`), UI dengan **WPF-UI** (Fluent, Mica, NavigationView).
- Alasan: `ReadProcessMemory`/`VirtualQueryEx` langsung dari C# tanpa jembatan; WPF-UI memberi tampilan modern tanpa desain dari nol.
- **Core terpisah dari UI** (`Ramreader.Core`, tanpa dependensi UI) agar trainer memakai Core yang sama.
- **State bersama** lewat `GameSession` (singleton via DI). Halaman juga singleton supaya hasil scan tidak hilang saat pindah menu.
- Kontrol WPF-UI harus dipakai bila ada (mis. `ui:ListView`, bukan `ListView`); kontrol WPF biasa tidak mengikuti tema dark.
- Read-only: handle dibuka hanya dengan `VM_READ | QUERY_INFORMATION`.

## 5. Struktur folder

Kondisi sekarang:

```
D:\Projects\Ramreader\
  Ramreader.sln
  .gitignore
  SPEC.md
  src\
    Ramreader.Core\                 (classlib, net10.0)
      Models\
        ModuleInfo.cs               record ModuleInfo(Name, BaseAddress, Size) + Contains()
        ProcessInfo.cs              record ProcessInfo(Id, Name, WindowTitle)
        MemoryRegion.cs             record struct MemoryRegion(BaseAddress, Size, Protect, IsWritable)
        ScanValueType.cs            enum Int32, Float, Int64, Double
        ScanMode.cs                 enum Exact, Changed, Unchanged, Increased, Decreased
        ScanHit.cs                  record struct (Address, Value, Previous), nilai = bit mentah ulong
        SavedAddress.cs             alamat tersimpan: Label, ValueType, VerifiedRounds, MainModuleSize, Chains
        GameProfile.cs              isi file ekspor untuk trainer (FormatVersion, GameName, Addresses)
      Memory\
        NativeMethods.cs            P/Invoke OpenProcess, ReadProcessMemory, VirtualQueryEx, CloseHandle
        ProcessMemory.cs            pembaca memori read-only
        ProcessLister.cs            daftar proses yang punya jendela
      Scanning\
        ValueCodec.cs               parse / format / compare per tipe
        ValueScanner.cs             first scan + next scan
      Pointers\
        PointerChain.cs             record + Serialize / TryParse / Describe
        PointerScanner.cs           BFS multi-level + filter bloom per halaman 4 KB
        PointerVerifier.cs          Resolve(chain) + Filter(chains, target)
    Ramreader.App\                  (WPF, net10.0-windows)
      App.xaml / App.xaml.cs        tema Dark; DI container (App.Services); memuat Themes\AppStyles.xaml
      Themes\
        AppStyles.xaml              SEMUA gaya tampilan (AppPage, PageRoot, PageTitle, CardBorder, FieldLabel, ColumnHeader, MutedText)
      MainWindow.xaml / .cs         FluentWindow, Mica, NavigationView 5 menu, bilah status
      Views\
        ProcessPage, ScanPage, PointerPage      fungsional
        SavedPage                               fungsional (kartu per alamat)
        SignaturePage                           placeholder
      ViewModels\
        ProcessViewModel.cs
        ScanViewModel.cs, ScanRow.cs
        PointerViewModel.cs, ChainRow.cs
        SavedViewModel.cs           SavedItem (kartu) + SavedViewModel (halaman Tersimpan)
      Services\
        GameSession.cs              Memory, IsConnected, StatusMessage, TargetAddress
        ChainStore.cs               %AppData%\Ramreader\chains\<namaproses>.txt
        SavedStore.cs               %AppData%\Ramreader\sessions\<namaproses>.json + tulis file ekspor
```

Rencana tambahan (penamaan usulan):

```
Core\Scanning\   ValueScanner.cs, ScanResult.cs, ScanType.cs, ValueType.cs, AobScanner.cs, SignatureMaker.cs
Core\Pointers\   PointerScanner.cs, PointerChain.cs, PointerVerifier.cs
Core\Models\     SavedAddress.cs, GameProfile.cs
App\Services\    SessionStore.cs (JSON di %AppData%\Ramreader\sessions\), ExportService.cs
App\ViewModels\  ScanViewModel, PointerViewModel, SignatureViewModel, SavedViewModel
src\Ramreader.Trainer\   (fase 2, referensi ke Core)
```

## 6. Logika yang sudah dibangun

### 6.1 Core: `ProcessMemory`
- `static ProcessMemory Attach(int processId)`: melempar `InvalidOperationException` dengan pesan ramah (error Windows 5 = akses ditolak: butuh Administrator atau game dilindungi anti-cheat).
- Properti: `ProcessId`, `ProcessName`, `Modules` (IReadOnlyList<ModuleInfo>), `MainModule` (= Modules[0], exe game), `IsAlive`.
- `RefreshModules()`, `byte[]? Read(ulong address, int size)` (null jika gagal), `bool TryRead<T>(ulong, out T) where T : unmanaged`, `ulong? ReadPointer(ulong)`.
- `IEnumerable<MemoryRegion> EnumerateRegions(bool writableOnly)`: lewat `VirtualQueryEx` sampai 0x7FFFFFFFFFFF; hanya region commit, bukan guard page; mask readable `0xEE`, writable `0xCC`.
- `IEnumerable<(ulong Address, byte[] Data)> ReadChunks(bool writableOnly, int chunkSize = 16MB)`: dasar semua scanner.
- `IDisposable` (menutup handle).

### 6.2 App: `GameSession` (ObservableObject, singleton)
- `Memory` (ProcessMemory?), `IsConnected`, `StatusMessage`.
- `Connect(int pid)`, `Disconnect(string? message)`.
- `DispatcherTimer` 1,5 detik (watchdog): jika game mati, otomatis disconnect dengan pesan "Game ditutup...". Ini fondasi deteksi restart game untuk alur verifikasi.

### 6.3 App: `ProcessViewModel` + `ProcessPage`
Daftar proses (`ProcessLister.GetRunning()`), pencarian, `RefreshCommand`, `ConnectCommand` (dobel-klik juga), `DisconnectCommand`, `ErrorMessage`/`HasError` untuk `ui:InfoBar`. Kartu info game terhubung. Bilah status di `MainWindow` ter-bind ke `GameSession` (titik hijau saat terhubung).

### 6.4 DI & navigasi
`App.Services` (static `IServiceProvider`). Semua singleton: `GameSession`, `ProcessViewModel`, `ScanViewModel`, `PointerViewModel`, `ChainStore`, dan 5 Page. `MainWindow`: `DataContext = GameSession`, `RootNavigation.SetServiceProvider(App.Services)`, `Navigate(typeof(ProcessPage))` saat Loaded. Setiap halaman baru: daftarkan di `App.xaml.cs` dan di `NavigationView.MenuItems`.

## 7. Desain algoritma (belum dibangun, ikuti ini)

Semua operasi berat berjalan di `Task` latar dengan `IProgress<double>` dan `CancellationToken`; UI tidak boleh beku. Semua scanner pakai `ReadChunks`.

### 7.1 Cari nilai (Langkah 5), SUDAH DIBANGUN, lihat 11.1
- Tipe: int32, float (nanti: int64, double, 2/1 byte). Penyelarasan 4 byte untuk int/float.
- First scan: exact value di region **writable** (tempat data game). Simpan daftar alamat (cap ~20 juta).
- Next scan: baca ulang tiap kandidat, saring. Mode: exact, berubah, tidak berubah, bertambah, berkurang. Untuk mode relatif perlu menyimpan nilai sebelumnya (struct alamat + nilai lama).
- UI: tipe nilai, input nilai, tombol First/Next/Reset, daftar hasil (alamat hex, nilai, nilai sebelumnya) dengan progres dan tombol batal; klik kanan atau tombol "Cari jalur stabil" mengirim alamat terpilih ke halaman Jalur Stabil.

### 7.2 Pointer scan + verify (Langkah 6), KODE TERPASANG, lihat 11.2
Tujuan: cari rantai `Game.exe+offset -> [+o1] -> [+o2] ...` menuju alamat target.
- Level BFS. Frontier awal = {target}. Tiap level: urutkan alamat frontier, scan semua region writable per 8 byte, untuk tiap nilai pointer `p` (0x10000 <= p <= 0x7FFFFFFFFFFF) cari alamat frontier terkecil >= p lewat binary search; jika `alamat - p <= maxOffset` (default 0x800) buat Node {Addr = lokasi pointer, Off = alamat - p, Parent = node frontier}.
- Jika lokasi pointer ada di **modul utama (exe)** -> chain statis, simpan. Selain itu -> masuk frontier level berikutnya (dedupe per alamat). Cap: frontier 150.000, hasil 300.000. Default depth 4.
- Serialisasi chain: `Modul|RelHex|off1,off2,...` (hex), offset diurut dari yang terdekat ke pointer statis menuju target.
- Evaluasi chain: `cur = ReadPtr(modBase + rel)`; untuk i: `cur += off[i]`; jika bukan offset terakhir `cur = ReadPtr(cur)`. Hasil akhir = alamat target (format ini sama dengan Cheat Engine).
- **Verify** (inti pengurangan restart): setelah game direstart, user mencari alamat target baru lewat scan singkat; verifikasi evaluasi ulang semua chain tersimpan dengan base modul baru, simpan hanya yang menghasilkan alamat baru. Ulang tiap restart sampai tersisa sedikit. Chain disimpan di sesi (JSON), bukan file sementara.
- Hanya modul utama dianggap statis (kurangi noise); opsi menambah DLL lain nanti. Keterbatasan: satu node hanya cocok ke satu target frontier terdekat.
- UX: setelah game ditutup, watchdog memunculkan petunjuk "Buka game lagi, cari nilainya lagi, lalu klik Verifikasi".

### 7.3 AOB dan signature (Langkah 7)
- Pola `48 8B ?? 58` dengan wildcard; scan modul utama per region (region ∩ rentang modul), chunk 16MB dengan overlap `pat.Length-1` (jangan hitung ganda).
- `SignatureMaker`: dari alamat instruksi, coba panjang 8,12,16,20,24,32,40,48 byte persis, ambil yang pertama unik di modul. User dapat mengganti byte displacement dengan `??` manual (tanpa disassembler pada versi awal).
- Resolver RIP-relative untuk trainer: `mov rax,[rip+disp32]` -> alamat statis = alamat_instruksi + panjang_instruksi + disp32.
- Alamat instruksi saat ini masih dari Cheat Engine ("find out what writes"). Opsi masa depan: debugger hardware breakpoint di Ramreader.

### 7.4 Tersimpan, sesi, ekspor (Langkah 8)
- Sesi per game (nama proses + versi/hash exe) di `%AppData%\Ramreader\sessions\<game>.json`: alamat tersimpan (label, tipe, chain atau signature, catatan), chain kandidat, riwayat.
- Ekspor profil game (JSON) untuk trainer: lihat 1.5.

## 8. Prinsip UX

- Alur terpandu; bilah status selalu memberi **langkah berikutnya**.
- Bahasa Indonesia, istilah teknis dijelaskan singkat.
- Tidak ada UI beku; semua operasi panjang punya progres + batal.
- Pesan error ramah dan menyarankan solusi (mis. jalankan sebagai Administrator).
- Dark theme, Mica, ikon Fluent (`ui:SymbolIcon`), `ui:InfoBar`, `ui:Button`, `ui:TextBox`, `ui:ListView`.
- Navigasi samping: Proses, Cari Nilai, Jalur Stabil, Signature, Tersimpan.

## 9. Status

| Langkah | Isi | Status |
|---|---|---|
| 1 | Scaffold solusi, paket, folder | Selesai |
| 2 | Core: attach, baca memori | Selesai (terbukti: byte awal `MZ`) |
| 3 | Tema Fluent, jendela utama, navigasi | Selesai |
| 4 | Halaman Pilih Game + GameSession + bilah status | Selesai (`ui:ListView` sudah diperbaiki; hubungkan lewat tombol atau klik dua kali bekerja) |
| 5 | Halaman Cari Nilai | Selesai (terbukti: scan int jumlah tiket NSUNSC, Scan Lanjutan, Jadikan Target). Kegagalan awal terjadi karena Ramreader tersambung ke msedge, bukan ke game |
| 6 | Pointer scan + verify (halaman Jalur Stabil) | Selesai (terbukti: 16 jalur diperiksa, 15 bertahan setelah restart game; jalur langsung NSUNSC.exe+21DA00C ikut bertahan). Kode di 11.2, desain di 7.2 |
| 5b | Scan nilai awal tidak diketahui + scan rentang nilai (between) | Snapshot terbukti (HP NSUNSC float ketemu, 11 jalur, terverifikasi 1x). Scan Rentang: kode terpasang, belum dites. Lihat 11.5 |
| 7 | AOB + signature | Kode terpasang, belum dites. Lihat 11.6 |
| 8 | Tersimpan, sesi JSON, ekspor | Terbukti: Simpan Alamat, kartu Tersimpan membaca nilai tiket NSUNSC secara langsung (ikut berubah di game). Belum dites: Ekspor Profil (klik, lalu cek isi file JSON) |
| 8b | Tata letak baru + tema terpusat | Selesai (lihat 11.4). Judul halaman putih terbukti di Proses dan Signature; semua halaman memakai Themes\AppStyles.xaml |
| 9 | Trainer (`Ramreader.Trainer`) | Belum |

## 10. Catatan dan risiko

- Semua kode ditulis AI tanpa bisa dijalankan; sebelumnya aman, tapi nama API WPF-UI 4.3.0 bisa berbeda. Bila error, minta pesan persis.
- Anti-cheat/Denuvo (mis. Monster Hunter Wilds) bisa memblokir `OpenProcess` atau mendeteksi akses; sarankan latihan di game single-player tanpa proteksi dulu. Jangan dipakai di game online.
- Efek Mica memberi rona warna dari wallpaper; itu normal.
- Prototipe konsol awal (hanya di sandbox AI, bukan di proyek user) sudah dipindahkan ke desain bagian 7.

## 11. Rincian implementasi Langkah 5 dan 6 (lanjutan bagian 6)

### 11.1 Cari Nilai (Langkah 5)
Core:
- Nilai disimpan sebagai **bit mentah `ulong`** (`ScanHit`) supaya semua tipe memakai struktur yang sama. `ValueCodec`: `SizeOf`, `TryParse` (koma desimal diterima), `Format`, `Compare`, `ReadRaw`.
- `ValueScanner`: `FirstScan` (region writable lewat `ReadChunks`, selaras per ukuran tipe, batas `MaxHits` = 5 juta + flag `Truncated`), `NextScan` (kandidat berurutan dibaca per blok <= 1 MB, bukan satu-satu), `ReadCurrent`, `Reset`. Semua mendukung `IProgress<double>` dan `CancellationToken`.

App:
- `ScanViewModel` (singleton): scan jalan di `Task.Run`; tombol Batal; `DispatcherTimer` 1 detik memperbarui nilai maksimal 300 baris yang tampil; hasil di-reset otomatis saat `GameSession.Memory` berganti; hint langkah berikutnya berubah sesuai jumlah hasil.
- "Jadikan Target" mengisi `GameSession.TargetAddress` (dikosongkan saat `Disconnect`).
- Mode scan lanjutan: nilai tepat, berubah, tidak berubah, bertambah, berkurang. Tipe nilai terkunci setelah scan pertama.

### 11.2 Jalur Stabil (Langkah 6)
Core (`Ramreader.Core.Pointers`):
- `PointerChain(ModuleName, ModuleOffset, Offsets)`. Jika `Offsets` kosong, alamat = `modul+ModuleOffset` (statis langsung). Jika tidak: baca pointer di `modul+ModuleOffset`, tambah `Offsets[0]`; kecuali itu offset terakhir, baca pointer di hasilnya dan tambah offset berikutnya. Hasil akhir = alamat target. Serialisasi satu baris: `Modul|RelHex|off1,off2` (hex).
- `PointerScanner.Scan(memory, target, maxDepth, maxOffset, progress, ct)`: BFS per level atas semua region writable, 8 byte per langkah. Pencarian cepat: filter bloom per halaman 4 KB (2 MB bit array) lalu binary search atas alamat frontier terurut. Lokasi pointer di **modul utama** menjadi hasil; lainnya masuk frontier level berikutnya (dedupe + `visited` untuk mencegah siklus). `MaxFrontier` = 100.000, `MaxResults` = 100.000, hasil diurutkan terpendek dulu.
- `PointerVerifier`: `Resolve` (evaluasi chain dengan base modul sekarang) dan `Filter` (sisakan chain yang `Resolve == target baru`).

App:
- `ChainStore`: file teks per nama proses, baris pertama `#rounds=N`, sisanya chain. Kandidat bertahan setelah aplikasi ditutup dan game direstart.
- `PointerViewModel` (singleton): opsi kedalaman 2-5 (default 4) dan jarak offset 0x400 / 0x800 (default) / 0x1000 / 0x2000.
  - **Cari Jalur** menimpa kandidat lama dan menyimpannya; hasil nol tidak menimpa kandidat lama.
  - **Verifikasi** ditolak kalau PID sama dengan PID pencarian/verifikasi terakhir (game belum direstart). Kalau tidak ada chain yang bertahan, kandidat lama TIDAK dihapus (kemungkinan user memilih alamat target yang salah) dan PID terakhir tidak diperbarui, jadi user bisa langsung mencoba alamat lain.
  - Kandidat dimuat ulang dari file saat terhubung ke proses dengan nama berbeda dari yang ada di memori.
  - Kolom "Alamat sekarang" = hasil `Resolve` untuk 200 chain teratas.
- DI: `ScanViewModel`, `PointerViewModel`, `ChainStore` terdaftar singleton di `App.xaml.cs`.

### 11.3 Tersimpan (Langkah 8)
Core (`Ramreader.Core.Models`): `SavedAddress` (Label, ValueType, VerifiedRounds, MainModuleSize, SavedAt, Chains = baris `PointerChain.Serialize()`, terbaik pertama, maksimal 20) dan `GameProfile` (FormatVersion = 1, GameName, ExportedAt, Addresses).

App:
- `SavedStore`: `%AppData%\Ramreader\sessions\<namaproses>.json`; `Export` menulis file ke lokasi pilihan user lewat `SaveFileDialog` (tidak membuat file otomatis).
- `SavedViewModel` (singleton): memuat daftar saat terhubung ke proses dengan nama berbeda; timer 1 detik memperbarui nilai tiap kartu. Tiap kartu mencoba jalurnya berurutan sampai ada yang terbaca; kalau jalur utama gagal, dipakai jalur cadangan #n dan statusnya tertulis di kartu. Perintah: Hapus, Salin Jalur (jalur utama), Ekspor Profil.
- `GameSession.TargetType` diisi oleh `ScanViewModel.SetTarget` supaya alamat tersimpan tahu tipenya.
- Halaman Jalur Stabil: tombol Simpan Alamat membuka kotak label di tempat (Enter = simpan). Label yang sama menggantikan yang lama. Jalur yang belum diverifikasi boleh disimpan dengan peringatan.
- Temuan uji: kolom "Alamat sekarang" yang tampil "-" setelah restart dulu BUKAN jalur putus, melainkan kolom hanya diperbarui lewat `ShowRows()` (data basi). Pesan Verifikasi sekarang melaporkan berapa jalur yang bisa dibaca di game saat ini.

### 11.4 Tata letak dan tema
- Semua halaman: bagian atas tetap di tempat (judul, info bar, kartu kontrol dengan SEMUA tombol, ringkasan, judul kolom). Hanya `ui:ListView` di bawahnya yang digulir (Grid dua baris Auto/*, `ScrollViewer.VerticalScrollBarVisibility="Disabled"` lewat gaya `AppPage`).
- Semua gaya ada di `Themes\AppStyles.xaml` (setara CSS global): `AppPage`, `PageRoot`, `PageTitle`, `CardBorder`, `FieldLabel`, `ColumnHeader`, `MutedText`. Untuk mengubah tampilan (sudut kartu, jarak halaman, warna judul), ubah file itu saja, jangan menulis ulang per halaman.
- Judul sempat tampil hitam karena pembungkus gulir bawaan WPF-UI tidak lagi ikut memberi warna ke teks; gaya `AppPage` sekarang mengisi `Foreground` eksplisit. Belum dikonfirmasi di layar.
- Hal yang masih tertulis per halaman (belum dipusatkan): jarak antar tombol (`Margin="8,0,0,0"`), lebar kolom daftar.

### 11.5 Snapshot dan scan rentang (Langkah 5b)
- `ValueScanner.FirstScanUnknown` (tombol Ambil Snapshot) dan `FirstScanRange` (tombol Scan Rentang) memakai `CaptureSnapshot`: blok memori + bitmask kandidat (`BitArray`), batas `MaxSnapshotBytes` = 3 GB (flag `SnapshotTruncated`). Selama kandidat > `MaxHits` (5 juta) disimpan padat (`IsSnapshot`, daftar `Hits` kosong); begitu <= 5 juta otomatis jadi daftar alamat biasa.
- `ScanMode.Between` + `ValueCodec.InRange` (inklusif, NaN tidak masuk, batas ditukar otomatis kalau terbalik). `NextScan` menerima parameter opsional `upper`.
- Pembatalan di tengah scan lanjutan mode snapshot membuang snapshot (blok lama dilepas bertahap supaya RAM tidak dobel); ambil snapshot lagi.
- UI: kolom `Sampai (rentang)` di sebelah kolom Nilai; mode lanjutan "Nilai dalam rentang".

### 11.6 Signature (Langkah 7)
Core: `Models\SignatureRef` (Pattern, DispOffset, InstructionEnd, StaticDelta, Offsets, Note); `SavedAddress.Signatures` (maks 5 per alamat, ikut di JSON ekspor). `Scanning\`: `AobPattern` (parse/Create, wildcard `??`), `AobScanner` (modul utama, chunk 16 MB + overlap, tanpa hasil ganda), `RipReferenceScanner` (cari instruksi `[rip+disp32]` yang merujuk alamat statis jalur: ModRM mod=00 rm=101, tebakan ukuran immediate dari opcode), `SignatureMaker` (`MakeExact` dari alamat instruksi, 8..48 byte persis; `MakeForReference` jendela terpendek yang unik dengan disp32 jadi `??`), `SignatureResolver` (pola -> disp32 -> alamat RIP + StaticDelta -> ikuti Offsets; lokasi pola di-cache per PID, pemindaian pertama di latar).
App: `SignatureViewModel` + `SignaturePage` (pilih alamat tersimpan -> Cari Kode Pengguna -> Buat Signature -> Uji Pola -> Simpan ke Alamat; Tes Signature; Buat dari Alamat = jalur Cheat Engine). Simpan menolak pola yang tidak unik atau yang alamatnya beda dari jalur utama. `SavedItem.Update` memakai signature kalau semua jalur putus.
Keterbatasan: pola yang memuat instruksi relatif lain (call/jmp rel32) bisa patah setelah update; hanya terbukti nyata kalau game di-update. Hanya jalur berakar di modul utama. Signature dari "Buat dari Alamat" hanya menemukan lokasi kode (bahan patch kode fase 2).

## 12. Catatan lapangan dan backlog

- Game uji bebas (user bukan hanya satu game). Untuk uji pipeline tanpa risiko: Cheat Engine Tutorial (Tutorial-x86_64.exe) atau game single-player apa pun. Coba nilai bilangan bulat dulu (uang, amunisi) sebelum float.
- Scan float HP gagal pada percobaan pertama. Penyebab umum: game menyimpan sebagai int/double atau dengan skala lain (0-1, persen); nilai di layar dibulatkan sehingga bukan nilai mentah; nilai mentah tidak persis sama dengan yang tampil. Scan "tepat sama" tidak bisa menolong di kasus ini.
- Backlog berurutan prioritas:
  1. Langkah 5b (SELESAI, lihat 11.5): snapshot + scan rentang.
  2. Pointer scan dengan banyak target sekaligus (user belum tahu mana alamat yang benar dari beberapa kandidat).
  3. Opsi menambah DLL lain sebagai "modul statis" di pointer scan.
  4. Debugger sederhana (hardware breakpoint) untuk "find out what writes to this address" agar alamat instruksi tidak perlu dari Cheat Engine.
  5. Tipe 1/2 byte, array of bytes, hex viewer.
- Pelajaran teknis: kontrol WPF biasa tidak mengikuti tema gelap; selalu pakai kontrol WPF-UI (`ui:ListView`, `ui:Button`, `ui:TextBox`) bila ada. `ComboBox` masih WPF biasa; ganti kalau terlihat putih.
- Proyek WPF TIDAK memuat `System.IO` dan `System.Net.Http` otomatis (SDK WPF mengurangi implicit using). Setiap file di Ramreader.App yang memakai `File`, `Path`, atau `Directory` harus punya `using System.IO;`. Ramreader.Core tidak terpengaruh.
- Build gagal dengan MSB3027/MSB3021 ("file is locked by Ramreader.App") bukan error kode: aplikasi masih terbuka dari `dotnet run` sebelumnya. Tutup jendelanya atau jalankan `Get-Process Ramreader.App -ErrorAction SilentlyContinue | Stop-Process -Force`, lalu `dotnet build`. Kompilasi C# terjadi sebelum langkah salin, jadi kalau hanya error ini yang muncul, kodenya kemungkinan besar sudah lolos kompilasi.
- Scan "gagal" (hasil 0 atau angka acak seperti -842150451) pertama-tama cek bilah status: Ramreader mungkin tersambung ke proses yang salah (pernah ke msedge). Angka kecil seperti jumlah tiket muncul di banyak tempat; pakai nilai yang panjang/unik lebih dulu (mis. uang), lalu Scan Lanjutan.
- Alamat yang sama persis di dua sesi game dan berawalan 0x7FF7 (area exe) biasanya alamat statis: Pointer scan menemukannya sebagai jalur "langsung" (modul+offset), tanpa pointer berantai. Yang membuktikan stabil adalah angka offset dari modul, bukan alamat mentahnya.
- Pembaruan SPEC.md selalu pakai Sebelum/Sesudah (lihat aturan 13 di bagian 2).