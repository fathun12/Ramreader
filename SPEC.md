# SPEC: Ramreader

> Dokumen serah-terima untuk sesi AI berikutnya. Baca SELURUHNYA sebelum menulis apa pun.
> Terakhir diperbarui: 2 Nov 2026. Status: Langkah 1-8 terbukti jalan di NSUNSC. Langkah 9 (trainer): Paket A dan B terbukti. Paket C: kode terpasang, hotkey belum dites. Paket D (code patch): debugger Penulis Kode terbukti menangkap instruksi penulis. NOP polos terbukti membuat HP tidak turun TAPI musuh ikut kebal (dilaporkan user), dan chakra masih berkurang. Perbaikan 9De (code cave khusus pemain, bawaan semua patch; NOP polos hanya kalau Mode = Nop): TERBUKTI di NSUNSC, HP pemain tidak berkurang dan HP musuh tetap berkurang (dilaporkan user). Chakra: TERBUKTI di NSUNSC. Patch "Unlimited Chakra" sekarang di +94BA22 (movss [rcx+18h],xmm0, F3 0F 11 41 18 diganti cave pemain-saja); chakra tidak turun saat dinyalakan (dilaporkan user). Label yang sama menggantikan patch lama +94BC9B, yang memang pengisian. Lihat bagian 9, 11.10 dan 11.11 (11.11 = pekerjaan penanda sisi/ID karakter, yang sedang berjalan).

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
4. **Folder dan file baru dibuat lewat terminal, bukan disalin manual.** Beri satu perintah **PowerShell 5.1** untuk terminal VS Code di root proyek (hindari `&&`; pakai baris terpisah atau `;`) yang membuat folder dan menulis SETIAP file baru memakai here-string dan `[IO.File]::WriteAllText(path, isi, (New-Object Text.UTF8Encoding $false))` (UTF-8 tanpa BOM, supaya karakter seperti bullet tidak rusak). Jangan menempel isi file baru sebagai blok kode untuk disalin satu per satu. Blok Sebelum/Sesudah hanya untuk mengubah file yang sudah ada.
5. **Kode di chat hanya berupa Sebelum/Sesudah untuk file yang sudah ada.** Isi file baru TIDAK ditempel untuk disalin; isinya masuk ke perintah terminal pada aturan 4 (AI tidak punya akses ke disk user, jadi perintah itulah yang membuat file).
6. **Format perubahan kode:**
   - File baru/utuh: dibuat lewat perintah terminal (aturan 4); isinya ada di dalam perintah itu, path lengkap tertulis di sana.
   - Mengubah/menambah kode yang sudah ada: format **Sebelum / Sesudah** (blok "Sebelum" harus persis ada di file user).
7. **Beri label langkah bersambung** (4a, 4b, 4c...) supaya user bisa bilang "4c sudah, 4d gagal".
8. **Satu langkah besar per giliran**, diakhiri instruksi build/tes dan apa yang harus user laporkan. User akan menempel output terminal atau screenshot.
9. **Jujur soal kode yang belum diuji.** AI tidak bisa menjalankan kode user; sebutkan bagian yang paling mungkin bermasalah (terutama nama API WPF-UI).
10. **Maksimal satu pertanyaan per respons**, dan hanya kalau benar-benar menentukan arah.
11. Saat error: minta pesan error persis + isi file terkait, lalu perbaiki dengan format Sebelum/Sesudah. Jangan menebak berlebihan; kadang penyebabnya user salah menempel ke file (xaml vs xaml.cs tertukar pernah terjadi).
12. UI harus bagus dan UX bersih; keputusan teknologi diserahkan ke AI selama ramah user.
13. Aturan Sebelum/Sesudah berlaku juga untuk SPEC.md dan file non-kode. Jangan pernah menulis "ganti bagian ini dengan ini" atau "tambahkan di akhir" tanpa teks Sebelum yang persis ada di file. Edit kecil saja; jangan menyuruh user memindah blok besar, lebih baik ganti penomoran.

14. **Meminta file atau menyuruh menjalankan perintah:** jelaskan dulu masalahnya dan alasannya, lalu beri satu perintah PowerShell yang menyalin isi file ke clipboard (`Set-Clipboard`). Jangan membuat file di Desktop atau di mana pun, dan kumpulkan semua yang dibutuhkan dalam satu pesan. Jangan memakai istilah samar ("memblokir", "kutahan").

15. **Jangan menunda perapian SPEC.md.** Setiap kali sesuatu terbukti atau berubah, semua tempat yang menyebutnya (header "Terakhir diperbarui", tabel bagian 9, rincian bagian 11, catatan lain) diperbarui dalam respons yang sama. Jangan menulis "dirapikan belakangan": AI sesi berikutnya hanya membaca SPEC dan akan menganggap yang sudah selesai sebagai belum. Jangan mencatat "terbukti" sebelum user melaporkan hasil dari layarnya sendiri.

16. **Menggantikan aturan 4 dan 5 (pembuatan file).** Folder dan file KOSONG dibuat lewat satu perintah PowerShell (New-Item). Isi kodenya dikirim langsung di chat sebagai blok yang user paste sendiri ke file kosong itu. Abaikan semua petunjuk di aturan 4 dan 5 soal here-string dan WriteAllText.
17. **Jangan menunggu disuruh dua kali.** Kalau user sudah menyuruh meminta file atau melakukan sesuatu, lakukan di respons yang sama (perintah Set-Clipboard langsung diberikan). Jangan bertanya "mau lanjut?" untuk hal yang sudah diperintahkan.
18. **Laporan user dari pengalamannya sendiri adalah fakta.** Jangan membantah dengan "kemungkinan tidak perlu" atau tebakan. Pisahkan jelas mana yang terbukti dari layar user dan mana tebakan, dan beri label pada tebakan. Kalau user bilang sesuatu tidak jalan, anggap benar dan cari penyebabnya.
19. **Baca pesan dan screenshot sampai habis sebelum menjawab, dan jangan mengubah arti kalimat user.** Contoh yang pernah salah: "musuhnya tidak mau ganti karakter" berarti CPU tidak mau tag, bukan daftar objek yang tidak berubah.
20. **Jangan pernah menyuruh Mode Nop** untuk instruksi yang juga dipakai musuh. NOP polos membuat musuh ikut kebal (terbukti untuk HP dan gauge summon). Mode Nop hanya jika user sendiri memintanya.
21. **Edit SPEC.md harus persis.** Kalau tidak yakin teks Sebelum ada di file user, minta potongan SPEC lewat perintah Set-Clipboard dulu, jangan menebak. Jangan mencatat "terbukti" sebelum user melaporkan dari layarnya.

22. **Setiap kali menyuruh user mengetes, beri SATU perintah PowerShell yang menutup proses lama, build, lalu membuka Ramreader.App dan Ramreader.Trainer sekaligus**, di respons yang sama. Perintah baku: `Get-Process Ramreader.App,Ramreader.Trainer -ErrorAction SilentlyContinue | Stop-Process -Force`, lalu `dotnet build`, lalu `if ($LASTEXITCODE -eq 0) { Start-Process dotnet -ArgumentList "run","--project","src\Ramreader.App","--no-build" -WorkingDirectory (Get-Location) -WindowStyle Hidden; Start-Process dotnet -ArgumentList "run","--project","src\Ramreader.Trainer","--no-build" -WorkingDirectory (Get-Location) -WindowStyle Hidden }`. Terminal harus Administrator. Jangan hanya menulis "jalankan dotnet build lalu run".
23. **Jangan pernah mengedit JSON sesi dengan ConvertFrom-Json lalu ConvertTo-Json** (PowerShell 5.1 merusak format tanggal, dan profil hilang dari Trainer; terbukti 2 Nov 2026). Edit file JSON hanya lewat penggantian teks (`[regex]::Replace`), selalu buat cadangan `.bak` dulu, dan cetak jumlah penggantian.
24. **Jawaban pendek dan hasil dulu.** User sering kehabisan token dan sabar menipis. Mulai dengan jawaban atas pertanyaannya (bisa, tidak bisa, hasilnya apa), lalu detail seperlunya. Jangan menyuruh user mengirim screenshot berulang kalau ada cara yang bisa disalin; kalau tidak ada, tulis bahwa screenshot memang satu-satunya cara.

25. **Setiap fitur penanda sisi harus dites di minimal 3 kondisi berbeda (match sekarang, rematch, ganti karakter lalu match baru) sebelum dicatat terbukti.** Tes satu sesi tidak cukup: nilai +0x44 sudah terbukti berubah antar sesi dan pernah dalam satu proses. Jangan menyuruh user menguji hanya satu sesi lalu menyimpulkan.

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
  README.md
  LICENSE
  Directory.Build.props         nama produk, penulis, versi untuk semua proyek
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
        AddressResolver.cs          jalur berurutan, lalu signature (dipakai Ramreader dan Trainer)
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
    Ramreader.Trainer\              (WPF, net10.0-windows; satu trainer generik untuk semua game, lihat 11.7 dan 11.8)
      Themes\AppStyles.xaml         salinan dari Ramreader.App
      Services\                     TrainerSession.cs, ProfileStore.cs, MemoryWriter.cs, SettingsStore.cs, HotkeyService.cs
      ViewModels\                   MainViewModel.cs, FeatureItem.cs
      MainWindow.xaml / .cs
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
| 7 | AOB + signature | Selesai (terbukti di NSUNSC, alamat HP: Cari Kode Pengguna, Buat Signature, Simpan, dan Tes Signature setelah restart game menghasilkan alamat yang sama dengan jalur utama; kartu Tersimpan menampilkan jumlah signature). Cadangan saat jalur putus baru teruji setelah game di-update. Lihat 11.6 |
| 8 | Tersimpan, sesi JSON, ekspor | Terbukti: Simpan Alamat, kartu Tersimpan membaca nilai tiket NSUNSC secara langsung (ikut berubah di game). Ekspor Profil terbukti (JSON NSUNSC.profile.json valid) |
| 8b | Tata letak baru + tema terpusat | Selesai (lihat 11.4). Judul halaman putih terbukti di Proses dan Signature; semua halaman memakai Themes\AppStyles.xaml |
| 9A | Trainer: scaffold, `AddressResolver` di Core, Trainer membaca profil NSUNSC (HP, tiket) | Terbukti: Trainer membaca HP dan tiket NSUNSC. Kartu Tersimpan Ramreader setelah refactor 9c belum dilaporkan terpisah. Lihat 11.7 |
| 9B | Trainer: Set nilai dengan validasi + Freeze | Terbukti di NSUNSC: Set HP/tiket jalan, Freeze menahan HP di layar, stabil walau match diulang. Temuan: nilai tetap turun sesaat sebelum dikembalikan. Belum dites: tutup lalu buka ulang game. Lihat 11.8 |
| 9C | Trainer: pengaturan per fitur (nama, min/max, preset) + hotkey global + kecepatan kunci | Kode terpasang, belum dites. Lihat 11.9 |
| 9D | Trainer: god mode lewat code patch; debugger Penulis Kode di Ramreader, kartu God Mode di Trainer, UI Trainer didesain ulang | NOP polos: HP tidak turun tapi musuh ikut kebal (dilaporkan user). 9De code cave khusus pemain: terbukti untuk HP (pemain kebal, musuh tidak). Chakra: terbukti. Patch +94BA22 (label Unlimited Chakra, menggantikan +94BC9B) menahan chakra pemain. Lihat 11.10 |
| 9E | Trainer: patch berlaku untuk seluruh tim user, musuh tidak. Alat "Objek Karakter" di Ramreader untuk mencari penanda sisi/tim di dalam objek karakter; lalu CodeCave membaca penanda itu | Alat terbukti jalan di layar user (pemantauan, Bandingkan). Kandidat penanda: offset +0x44. Stabil lintas ronde terbukti (sesi 4). Belum terbukti: stabil setelah respawn atau match baru, semua musuh punya nilai sama (tanpa P2), dan gauge Team Tag punya penanda. CodeCave Mode Team (Mode = "Team", MarkerOffset 0x44) kode terpasang di CodeCave.cs, CodePatch.cs, PatchItem.cs. Tes di NSUNSC: patch melindungi tim DAN musuh (dilaporkan user), jadi tujuan belum tercapai; penyebab belum ditemukan. Lihat 11.11 |

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
Terbukti di NSUNSC (alamat HP): pola unik di modul utama, hasilnya sama dengan jalur utama, dan tetap benar setelah restart game (ASLR). Cadangan saat jalur putus belum pernah teruji karena game belum di-update.
Keterbatasan: pola yang memuat instruksi relatif lain (call/jmp rel32) bisa patah setelah update; hanya terbukti nyata kalau game di-update. Hanya jalur berakar di modul utama. Signature dari "Buat dari Alamat" hanya menemukan lokasi kode (bahan patch kode fase 2).

### 11.7 Trainer: dasar dan membaca (Langkah 9A)
- Keputusan desain: satu trainer generik (`src\Ramreader.Trainer`) untuk semua game. Isi tiap game = profil JSON dari Ramreader (data, bukan kode). Pengaturan trainer (nama tampil, min/max, hotkey; Paket C) disimpan terpisah dari profil di `%AppData%\Ramreader.Trainer\settings\<game>.json` berkunci Label alamat, supaya ekspor ulang profil tidak menghapusnya.
- Core: `Pointers\AddressResolver.Resolve(memory, chains, signatures, type)` mengembalikan `ResolveResult` (Status Ok/SignatureSearching/Failed, Source Chain/Signature, Index, Address, Raw). Ini satu-satunya tempat logika "coba tiap jalur berurutan, kalau semua putus coba signature"; dipakai `SavedItem.Update` (Ramreader) dan `FeatureItem.Update` (Trainer).
- Trainer: `TrainerSession` (koneksi baca, watchdog 1,5 detik), `ProfileStore` (membaca `%AppData%\Ramreader\sessions\*.json` dan file ekspor `GameProfile`), `FeatureItem` (satu alamat = satu fitur), `MainViewModel` (daftar profil, sambung otomatis ke proses yang namanya sama dengan nama profil, timer 1 detik, peringatan kalau ukuran exe beda dari `MainModuleSize`).
- UI: `MainWindow` satu halaman (FluentWindow, Mica) memakai `Themes\AppStyles.xaml` salinan dari Ramreader.App. Kalau gaya diubah di satu aplikasi, salin ke yang lain.

### 11.8 Trainer: Set nilai dan Freeze (Langkah 9B)
- `Services\MemoryWriter`: satu-satunya kode di solusi yang menulis ke game. Handle dibuka dengan `PROCESS_VM_WRITE | PROCESS_VM_OPERATION` saat Trainer terhubung. Kalau ditolak, pembacaan tetap jalan dan alasannya ada di `TrainerSession.WriterError`. Core dan Ramreader tetap read-only.
- Validasi sebelum menulis (sejak Paket C bisa diatur per fitur, lihat 11.9; bawaan 0 sampai 1.000.000.000): rentang aman 0 sampai 1.000.000.000, bukan NaN/Infinity. Nilai baru di luar rentang ditolak. Nilai sekarang di alamat juga harus di dalam rentang; kalau tidak, alamat dianggap salah dan tidak ada yang ditulis.
- Set: parse sesuai tipe (`ValueCodec.TryParse`), resolusi ulang alamat saat itu juga lewat `AddressResolver` (tidak memakai alamat lama), tulis, lalu baca ulang. Kalau hasil baca ulang beda, pesan menyebut game kemungkinan menimpa nilainya.
- Freeze (sejak Paket C berjalan di thread latar, lihat 11.9; semula `DispatcherTimer` 50 ms) menulis nilai kunci dengan resolusi ulang alamat di tiap tick (supaya tidak menulis ke alamat basi). Nilai kunci = isi kotak nilai, atau nilai sekarang kalau kotak kosong. Kunci ditahan (tidak menulis) selama nilai di alamat di luar rentang aman, dan lepas otomatis saat game putus.
- Hasil uji di NSUNSC: Set HP dan tiket berhasil; Freeze membuat HP di layar tidak berkurang dan stabil walau match diulang 3 kali. Temuan: nilai sebenarnya tetap turun sesaat lalu dikembalikan (Freeze bekerja setelah kejadian, tidak mencegahnya). Yang membuat nilai tidak pernah turun hanya code patch (Paket D). Belum dites: tutup lalu buka ulang game.
- Risiko diketahui: game bisa menyimpan nilai di dua tempat (nilai asli dan salinan tampilan), sehingga Set tampak tidak mempan atau ditimpa balik. Solusinya masuk tahap god mode lewat code patch (SPEC 1.5), bukan Paket B. Nama API `ui:ToggleSwitch` di WPF-UI 4.3.0 belum terverifikasi.

### 11.9 Trainer: pengaturan, hotkey, kecepatan kunci (Langkah 9C)
- Pengaturan per game di `%AppData%\Ramreader.Trainer\settings\<namaproses>.json` (`SettingsStore`: `GameSettings` = `FreezeIntervalMs` + `Features` berkunci Label alamat; `FeatureSettings` = DisplayName, Min, Max, Preset, HotkeySet, HotkeyFreeze, semuanya teks). Terpisah dari profil Ramreader supaya ekspor ulang profil tidak menghapusnya. Disimpan otomatis tiap kolom Pengaturan selesai diedit.
- Rentang aman per fitur dari Min/Max (bawaan 0 dan 1.000.000.000). Kalau Min/Max bukan angka atau Min > Max, semua penulisan fitur itu ditolak sampai diperbaiki.
- Preset: nilai untuk hotkey Set (kalau kosong dipakai isi kotak nilai). Tombol Set memakai isi kotak nilai, dan kalau kosong memakai Preset.
- Hotkey global (`Services\HotkeyService`, `RegisterHotKey` + hook WM_HOTKEY, MOD_NOREPEAT): dua per fitur, Set dan Kunci (toggle freeze). Format `F1`, `Ctrl+F2`, `Alt+Shift+K`. Didaftar ulang tiap pengaturan berubah. Gagal daftar (dipakai program lain atau fitur lain) tampil di baris status fitur.
- Kecepatan kunci (pengaturan per game): 50 / 20 / 10 (bawaan) / 5 ms. Freeze berjalan di thread latar (`MainViewModel.FreezeLoop`, `timeBeginPeriod(1)`), bukan `DispatcherTimer` (resolusi sekitar 15 ms). Daftar fitur untuk thread diganti sebagai array baru tiap profil berganti, jadi tidak pernah dienumerasi saat berubah.
- Keterbatasan: Freeze tidak mencegah nilai turun, hanya mengembalikannya. Selisih satu frame game (sekitar 16 ms di 60 fps) masih mungkin, termasuk serangan yang langsung membunuh. Nilai yang benar-benar tidak turun = Paket D (code patch), butuh alamat instruksi yang menulis HP ("find out what writes"): dari Cheat Engine, atau debugger hardware breakpoint di Ramreader (backlog 4).
- Belum terverifikasi: tampilan `Expander` bawaan WPF-UI di tema gelap, dan `KeyConverter` untuk tombol angka di hotkey.

### 11.10 God mode lewat code patch (Langkah 9D)
- Core `Debugging\`: `WriteTracer` (hardware breakpoint tulis DR0 di semua thread lewat DebugActiveProcess; instruksi penulis ditemukan dengan disassembler Iced, dicocokkan lewat alamat efektif; kolom Hit/Berubah/Turun membedakan penulis damage dari penulis rutin), `CodePatchMaker` (signature unik, byte instruksi dan operand relatif jadi ??), `CodePatchLocator` (cari lokasi, cek byte asli/patch). Model `CodePatch` di `SavedAddress.CodePatches`, ikut di profil ekspor.
- Ramreader: halaman Penulis Kode (Mulai Pantau, Berhenti, Buat Patch). Ramreader tetap tidak menulis memori game, tetapi selama Pantau ia menempel sebagai debugger dan mengubah debug register thread game; semua dilepas saat berhenti.
- Trainer: `MemoryWriter.WriteCode` (thread game dijeda, VirtualProtectEx, FlushInstructionCache). `PatchItem`: byte di lokasi harus sama dengan OriginalBytes (atau PatchBytes), kalau tidak ditolak ("game di-update"). Patch dikembalikan otomatis saat Trainer ditutup atau profil diganti. Hotkey patch di `GameSettings.Patches`.
- UI Trainer: satu area gulir (ItemsControl), kartu fitur dan kartu God Mode di satu daftar, pengaturan fitur di panel samping.
- Hasil uji di NSUNSC: Mulai Pantau menangkap 3 instruksi penulis HP (NSUNSC.exe+94BAE2, +94BAE3, +94BAE4; Hit 20, Berubah 11, Turun 5 sama di ketiganya, jadi kemungkinan tafsir berbeda dari byte yang sama di alamat berdekatan). Patch belum pernah tersimpan (CodePatches kosong di NSUNSC.json), jadi God Mode belum teruji. Pilih baris terpanjang (movss, 5 byte), bukan baris adc/movups yang hanya potongannya.
- Hasil uji final di NSUNSC: tiga baris penangkap (movss/movups/adc) ternyata satu instruksi; patch yang benar adalah movss 5 byte (OriginalBytes F3 0F 11 41 10, PatchBytes 90 x5). Dengan God Mode aktif HP tidak turun lagi di setiap hit (dilaporkan user, screenshot Trainer). Patch dari baris adc (3 byte) terpotong dan salah; pilih selalu baris terpanjang.
- Jebakan: Ramreader menyimpan ulang seluruh file sesi dari data di memorinya (Buat Patch, Simpan Alamat, dst). Mengedit file sesi lewat skrip hanya aman kalau Ramreader tertutup; kalau tidak, editan tertimpa. Pilih selalu baris movss terpanjang di Penulis Kode (movups/adc hanya potongan byte yang sama). Patch chakra NSUNSC: Unlimited Chakra = movss [rcx+18h],xmm1 di +94BC9B (F3 0F 11 49 18) tersimpan; +94BC94 (720 hit, tidak pernah turun) adalah pengisian, jangan di-NOP; +94BA22 belum dibuat; belum dites di game. Analisis dari NSUNSC.json: patch HP ([rcx+10h]) dan chakra ([rcx+18h]) memakai basis pemain yang sama (X+0x28), jadi logika slot cave benar; +94BC9B berbentuk "tulis hanya kalau lebih besar" (pengisian/batas), sehingga chakra turun lewat instruksi lain (+94BA22). Langkah berikut: Pantau alamat Chakra, cari baris Turun > 0 selain +94BC9B, Buat Patch dengan label berbeda. Terbukti di layar user (Penulis Kode): +94BA22 (movss [rcx+18h],xmm0, 5 byte) tertangkap dengan Turun 1; +94BC94 (Turun 0, Berubah 172) adalah pengisian. Patch di +94BA22 dibuat dengan label yang sama (Unlimited Chakra), menggantikan +94BC9B; terbukti menahan chakra di Trainer (dilaporkan user). Berikutnya: cari alamat Cooldown Tag Team, Team Gauge, dan Kawarimi lewat Cari Nilai (Snapshot + Berubah/Bertambah/Berkurang), tahan dengan Freeze, dan patch lewat Penulis Kode kalau Freeze terlambat.
- 9De (code cave khusus pemain): instruksi penulis ([base+disp]) diganti jmp ke cave 0x1000 byte di dekat kode game (MemoryWriter.AllocNear). Cave: pushfq, cmp base,[slot], kalau sama (pemain) lewati penulisan, kalau beda jalankan instruksi asli, popfq. Slot = alamat nilai pemain (dari jalur alamat pemilik patch) dikurangi displacement, diperbarui thread latar tiap tick. Core: Debugging\CodeCave.cs. CodePatch.Mode: kosong/Cave = pemain saja (bawaan), Nop = NOP polos. Syarat: instruksi >= 5 byte, basis register 64-bit, tanpa indeks dan tanpa RIP-relatif. Hasil uji user: NOP polos membuat musuh ikut kebal. Belum dites: cave di game.
- Keterbatasan: NOP polos membuat semua karakter yang memakai instruksi itu kebal (musuh juga) dan penyembuhan lewat instruksi yang sama ikut mati. Terbukti lagi pada gauge summon (Team Tag, NSUNSC.exe+94BB7A, movss [rax+2Ch],xmm0): NOP polos membuat musuh ikut punya summon tak terbatas (dilaporkan user). Jangan pakai Mode Nop untuk Team Tag; pakai cave pemain-saja (Mode kosong), belum dites untuk summon. Terbukti dari layar user: cave pemain-saja hanya melindungi satu objek (yang ditunjuk jalur alamat), jadi HP/chakra/summon hanya berlaku untuk karakter yang alamatnya dicari; teman satu tim (mis. Sasuke, Sakura) tidak ikut, dan summon butuh alamat per kombinasi (6). Rencana perbaikan: cave memeriksa penanda sisi/tim di dalam objek, bukan alamat satu objek; butuh alat Ramreader untuk membandingkan isi objek karakter tim vs musuh. Belum dibangun. Desain alat "Objek Karakter" (halaman baru Ramreader): breakpoint eksekusi pada instruksi penulis (mis. +94BA22) untuk mencatat rcx semua karakter, label Teman/Musuh oleh user, bandingkan ~0x800 byte antar objek untuk mencari offset penanda sisi/tim, lalu CodeCave membaca penanda itu. Langkah 16: build lolos; pemantauan terbukti di layar user (4-6 objek tertangkap, ribuan hit, objek milik user ditandai otomatis "Tim (aku)"). Fungsi Bandingkan TERBUKTI di layar user: setelah 2 ronde (3 Tim, 3 Musuh) tersisa 3 kandidat. Kandidat terkuat +0x44 (Tim 0xD70455F6, Musuh 0xC0C13E9D, semua musuh sama); +0x298 dan +0x364 (Tim 0, Musuh bervariasi) kemungkinan status, bukan penanda sisi. Terbukti dari layar user (sesi baru, PID berbeda): nilai +0x44 BERUBAH tiap sesi (Tim sebelumnya 0xD70455F6, sekarang 0xC0C13E9D; Musuh sebelumnya 0xC0C13E9D, sekarang 0x57FCDEE8), tapi offset +0x44 masih memisahkan Tim dan Musuh pada ronde 1 (kandidat sesi baru: +0x40, +0x44, +0x94, +0x674). Jadi profil hanya boleh menyimpan offset penanda, dan angkanya dibaca Trainer dari objek pemain saat berjalan. Dua sesi game (PID 24760 dan 29856) dibandingkan: hanya offset +0x44 yang memisahkan Tim dan Musuh di kedua sesi (+0x298/+0x364 hanya sesi 1; +0x40/+0x94/+0x674 hanya sesi 2; +0x94 kemungkinan setengah atas pointer heap 0x283). Belum terbukti: stabil dalam satu match lintas ronde/respawn, dan apakah gauge Team Tag ([rax+2Ch], basis objek tim) punya penanda serupa. Rencana: CodeCave memakai cmp dword [reg+44h], imm32 dengan immediate diperbarui thread Trainer dari objek pemain; belum dibangun. Isi: Core\Debugging\ObjectTracer.cs (breakpoint eksekusi DR0 + Resume Flag, mencatat register basis tiap hit), ObjectComparer.cs (offset yang seragam di Tim dan beda dari Musuh), halaman App "Objek Karakter" (CharacterObjectsViewModel/Page; peran Tim/Musuh, objek milik user ditandai otomatis dari alamat tersimpan dikurangi displacement, Bandingkan mengiriskan hasil antar ronde). Belum terbukti objek punya field penanda. CodeCave belum diubah. Perbaikan: code cave khusus pemain (9De, belum).

### 11.11 Penanda sisi/tim (ID karakter), Langkah 9E

**Masalah yang diselesaikan (dari user, bukan tebakan).** Cave pemain-saja (9De) hanya melindungi SATU objek: yang ditunjuk jalur alamat yang disimpan (alamat dikurangi displacement). HP, chakra, dan summon hanya berlaku untuk karakter yang alamatnya dicari. Teman satu tim (mis. Naruto, Sasuke, Sakura) tidak ikut. Tujuan user: satu patch berlaku untuk seluruh timnya dan tidak untuk musuh, tanpa mencari alamat berulang per karakter. Untuk summon user melaporkan tiap kombinasi (karakter A/B/C, pasangan kiri/kanan) punya profil berbeda, jadi saat ini butuh sekitar 6 alamat.

**Yang sudah terbukti di NSUNSC (dilaporkan user dari layarnya).**
- Cave pemain-saja: HP pemain tidak turun, HP musuh tetap turun (9De).
- Chakra: patch Unlimited Chakra di NSUNSC.exe+94BA22 (movss [rcx+18h],xmm0) menahan chakra. +94BC94 adalah pengisian (jangan disentuh).
- NOP polos membuat musuh ikut kebal (HP) dan ikut punya summon tak terbatas (gauge Team Tag, NSUNSC.exe+94BB7A, movss [rax+2Ch],xmm0). Jangan pakai Mode Nop.
- Gauge Team Tag (float 0 sampai 100, batas atas dipaksa kembali ke 100.0 oleh +94BDF6; +94BDEF adalah pengisi) jalurnya bertahan setelah restart game (13 dari 26 jalur).
- Alat Objek Karakter (Ramreader, halaman baru): build lolos; pemantauan menangkap 4 sampai 6 objek karakter dengan ribuan hit dan objek milik user ditandai otomatis; Bandingkan menghasilkan kandidat penanda.

**Cara alat bekerja.**
- Core Debugging: ObjectTracer.cs (hardware breakpoint EKSEKUSI di satu instruksi, mis. +94BC94, lalu mencatat register basis tiap hit, memakai Resume Flag), ObjectComparer.cs (membandingkan 0x800 byte per objek, per 4 byte, mencari offset yang seragam di seluruh Tim dan tidak pernah sama dengan Musuh).
- App: Views CharacterObjectsPage, ViewModels CharacterObjectsViewModel (objek milik user = alamat tersimpan dikurangi displacement instruksi; peran Tim/Musuh ditandai user; Bandingkan mengiriskan hasil antar ronde; Ulangi Irisan mengosongkannya).
- Jebakan pemakaian: Berhenti lalu Mulai Pantau lagi MENGOSONGKAN daftar objek dan semua tanda Tim/Musuh. Bandingkan hanya membaca isi objek pada detik klik, jadi gerakan CPU tidak mengganggu hasilnya. Objek hanya tertangkap dan kolom Nilai hanya berubah kalau karakter memakai atau mengisi chakra (instruksi +94BC94); ronde tanpa aktivitas chakra hasilnya identik dengan ronde sebelumnya. Tombol Ulangi Irisan tidak mereset (dilaporkan user, penyebab belum dicari); reset yang pasti jalan: Berhenti lalu Mulai Pantau.

**Temuan dari layar user.**
- Sesi game 1 (PID 24760), 3 Tim dan 3 Musuh, 2 ronde: kandidat +0x44 (Tim 0xD70455F6, semua Musuh 0xC0C13E9D), +0x298 dan +0x364 (Tim 0, Musuh bervariasi; kemungkinan status).
- Sesi game 2 (PID 29856), 3 Tim dan 3 Musuh, ronde 1: kandidat +0x40, +0x44 (Tim 0xC0C13E9D, Musuh 0x57FCDEE8), +0x94 (Tim 0, Musuh 0x283; kemungkinan setengah atas pointer heap), +0x674.
- Sesi game 3 (PID 28920), 3 Tim dan 3 Musuh, ronde 1: 3 kandidat: +0x3C (Tim 0xA2128926, Musuh 0x3B50D13E), +0x40 (Tim 0x80DB7929, Musuh 0xB894C494), +0x44 (Tim 0xD70455F6, Musuh 0x4039B583). Nilai Tim di +0x44 sama dengan Tim sesi 1.
- Nilai Musuh di +0x44 berbeda tiap sesi (nilai Tim sesi 1 dan 3 sama, sesi 2 beda). Hanya offset +0x44 yang memisahkan Tim dan Musuh di ketiga sesi. Kesimpulan: profil hanya boleh menyimpan OFFSET penanda; angkanya harus dibaca dari objek pemain saat game berjalan.
- Sesi game 4 (PID 26060), 3 Tim dan 3 Musuh, instruksi +94BC94: ronde 1 kandidat +0x10 (HP float, gugur di ronde 2), +0x40 (Tim 0x4039B583, Musuh 0xC0C13E9D), +0x44 (Tim 0xC0C13E9D, Musuh 0x4039B583). Ronde 2: +0x40 dan +0x44 bertahan dengan nilai sama. Ronde 3 dilaporkan identik dengan ronde 2 (4.684 hit, nilai sama), jadi kemungkinan tanpa perubahan di game. Nilai di +0x40 dan +0x44 bertukar antara Tim dan Musuh. Nilai +0x44 di empat sesi hanya dari empat angka: 0xD70455F6, 0xC0C13E9D, 0x4039B583, 0x57FCDEE8; 0xC0C13E9D jadi Musuh di sesi 1 tapi Tim di sesi 4, jadi angka tidak boleh di-hardcode. Uji dengan P2 dilakukan user (dilaporkan user, ronde 2 sesi 4).
- Sesi game 4 (PID 26060), uji dengan P2 (dilaporkan user), instruksi +94BC94, 3 Tim dan 3 Musuh. Ronde 1 ke ronde 2: kandidat +0x10 (HP float, gugur), +0x40, +0x44, +0x448 (Tim 0, Musuh bervariasi; kemungkinan status). Nilai di +0x44 bertahan lintas ronde dengan Tim 0xC0C13E9D dan Musuh 0x4039B583. Pada screenshot belakangan di proses yang sama (objek di 0x2390..., kemungkinan match baru, belum dikonfirmasi) nilainya berganti: +0x44 Tim 0x4039B583, Musuh 0xD70455F6, dengan 51 kandidat (sebagian besar noise: Tim 0, Musuh bervariasi). Kesimpulan: offset +0x44 konsisten memisahkan Tim dan Musuh di 4 sesi, tapi angkanya berganti (lintas sesi dan kemungkinan lintas match), jadi angka harus dibaca dari objek pemain saat game jalan.
- Kolom Nilai di halaman Objek Karakter memang selalu hidup (dibaca tiap 0,5 detik); objek dan Hit hanya muncul kalau instruksi yang dipantau jalan (+94BC94 = chakra). Ulangi Irisan sekarang menampilkan pesan konfirmasi di kotak Info (kode terpasang, belum dites).
- Daftar kandidat tidak bisa disalin; user mengirim screenshot. Usulan (belum dibangun): tombol Salin.
- Sesi game 5 (PID 12444), instruksi HP +94BAE2 (movss [rcx+10h]) terbukti tertangkap oleh pemindai (kolom Instruksi sebelumnya kosong, itu sebabnya HP gagal). 3 Tim, 3 Musuh, 3 ronde: kandidat +0x3C, +0x44, +0x7BC, +0x7C4. +0x44 bertahan: Tim 0xD70455F6, Musuh 0xC0C13E9D (satu nilai untuk ketiga Musuh; sama dengan sesi 1). +0x7BC/+0x7C4 (Tim 0x1A9, Musuh 0) kemungkinan noise. Penanda +0x44 terbukti ada di objek HP, bukan hanya chakra.
- Tombol "Tandai Otomatis" (kode terpasang, belum dites): menandai Tim bila [objek+0x44] sama dengan milik "Tim (aku)", Musuh sisanya.
- Penyebab "HP tidak berfungsi" di Objek Karakter: kolom Instruksi bawaan 94BC94 (chakra) kembali tiap Ramreader dibuka ulang, jadi alamat HP dipasangkan dengan instruksi chakra (terbukti dari screenshot user, sesi PID 12444). Perbaikan (kode terpasang, belum dites): kolom kosong = otomatis dari CodePatches milik alamat tersimpan lewat CodePatchLocator.Find; kolom dikosongkan tiap ganti alamat tersimpan.
- Sesi 6 (instruksi HP +94BAE2, objek 0x12B8..., karakter berganti): +0x44 Tim 0xD70455F6, Musuh 0xC0C13E9D, sama dengan sesi 5; 3 Tim dan 3 Musuh. Catatan: bila tanda dibuat Tandai Otomatis, Bandingkan +0x44 itu melingkar (tidak independen); bukti independen hanya jumlah 3 lawan 3. Nilai penanda berganti di sesi lain (sesi 4), jadi tetap dibaca dari objek pemain tiap tick. Sesi 7 (PID 5616, game dibuka ulang): instruksi HP +94BAE2, 3 Tim dan 3 Musuh, 2 ronde; kandidat bermakna +0x3C, +0x40, +0x44. +0x44 Tim 0x88A639ED, Musuh 0xD70455F6 (tadinya Tim di sesi 1,3,5,6). Nilai penanda berasal dari kumpulan kecil (0xD70455F6, 0xC0C13E9D, 0x4039B583, 0x57FCDEE8, 0x88A639ED); tiap sesi memilih dua. Pengelompokan Tim/Musuh cocok dengan HP bersama di kolom Nilai (dua karakter satu sisi bernilai sama), jadi tanda tidak melingkar. Tiga kondisi (match, rematch/ganti karakter, game dibuka ulang) kini terpenuhi untuk +0x44 pada tes Bandingkan.
Game crash setelah Ramreader ditutup paksa (Stop-Process -Force) saat memantau: dugaan, breakpoint DR0 tertinggal di thread game (belum terbukti). Selalu klik Berhenti sebelum menutup Ramreader.
- Tanda Tim/Musuh dibuat user dengan heuristik (kolom Nilai yang berubah), jadi ada kemungkinan satu objek salah tanda.

**Yang BELUM terbukti (jangan dicatat sebagai terbukti sebelum user melapor).**
1. Nilai +0x44 stabil setelah respawn atau match baru. (Lintas ronde Bandingkan dan lintas tiga karakter Tim sudah terbukti di sesi 4; respawn dan match baru belum dites.)
2. Semua karakter di sisi musuh punya nilai +0x44 yang sama. Dalam tes terakhir CPU musuh tidak mau tag, sehingga hanya ada satu musuh. Butuh P2 untuk menguji.
3. Gauge Team Tag/summon (basis objek tim, [rax+2Ch]) punya penanda serupa. Belum dicari.
4. Alat Objek Karakter hanya terbukti di instruksi chakra (+94BC94). Objek tertangkap hanya kalau instruksi itu jalan, yaitu setelah karakter memakai chakra (dilaporkan user; jangan menulis "langsung tertangkap"). Di instruksi HP (+94BAE2, movss [rcx+10h]) belum terbukti: tiap karakter harus kena hit dulu. Kolom Instruksi bawaan adalah 94BC94 (chakra); user mencoba alamat HP dan kena hit tanpa hasil, dugaan: kolom Instruksi belum diganti ke 94BAE2. Mode Team di NSUNSC dites: God Mode dan Unlimited Chakra melindungi tim DAN musuh (dilaporkan user), penyebab belum ditemukan. Penanda pemain terbaca 0xD70455F6 (PID 13384).

**Kendala user.** User hanya punya satu controller/stick, sedangkan menguji sisi musuh butuh P2. Dari pencarian web (diskusi komunitas Steam, belum dicoba user, bukan dokumentasi resmi): game ini dilaporkan tidak menerima keyboard + controller secara langsung. Opsi: keyboard splitter/remapper (mis. reWASD) untuk controller virtual, controller kedua, atau Steam Remote Play Together (dukungannya belum diverifikasi). Alternatif tanpa P2: tandai objek saat game dipause, atau pakai Menu Praktik. Alat tambahan yang sudah ditawarkan, belum dibangun, menunggu user: tombol "Bekukan" yang menyimpan isi semua objek pada satu detik yang sama.

**CodeCave Mode Team (kode terpasang 2 Nov 2026 atas perintah user, dites di game: melindungi tim dan musuh sekaligus (dilaporkan user), belum benar; patch diubah ke Mode "Team" lewat skrip di JSON sesi, tanpa UI).**
- Mode baru (mis. Mode = "Team"). Cave tidak membandingkan alamat objek dengan slot, melainkan isi [register+0x44] dengan nilai penanda milik pemain (cmp dword [reg+44h], imm32; tanpa register cadangan). Kalau sama, lewati penulisan; kalau beda (musuh), jalankan instruksi asli.
- Thread latar Trainer membaca penanda dari objek pemain tiap tick (alamat nilai pemain dikurangi displacement, lalu +0x44) dan menulisnya ke immediate itu, seperti slot sekarang. Profil menyimpan offset penanda, bukan angkanya.
- Syarat sama dengan cave lama: instruksi >= 5 byte, basis register 64-bit, tanpa indeks dan tanpa RIP-relatif. Perubahan menyentuh Core\Debugging\CodeCave.cs, Models\CodePatch.cs (field offset penanda), dan Trainer\ViewModels\PatchItem.cs.

**Langkah berikutnya untuk AI sesi baru.** Bandingkan ronde 2 terbukti (sesi 4). Tampilan sesi 4 ronde 2 menunjukkan satu nilai di kolom Musuh untuk +0x44 (bacaan atas tampilan, belum dipastikan dari kode). Uji akhirnya lewat CodeCave Mode Team di game (teman tidak berkurang HP-nya, musuh tetap berkurang). Tawarkan membangun CodeCave Mode Team; jangan membangun tanpa diminta.

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
- Repo: github.com/fathun12/Ramreader (branch main). CI build di `.github\workflows\build.yml` (windows-latest, .NET 10). About, topik, rilis, dan pengaturan repo diisi manual lewat browser (tidak memakai GitHub CLI; AI cukup menuliskan teksnya di chat). Tiap kali satu langkah terbukti: update SPEC.md, lalu commit dengan pesan gaya `feat: ... (Langkah N)` dan push.