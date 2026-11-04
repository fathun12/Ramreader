# SPEC: Ramreader

> Dokumen serah-terima untuk sesi AI berikutnya. Baca SELURUHNYA sebelum menulis apa pun.
> Terakhir diperbarui: 3 Nov 2026, akhir sesi rilis (BACA bagian 2f dulu, lalu aturan 26 sampai 33; 2e tetap berlaku kecuali 2f menggantikannya; Release v0.1.0 beta sudah terbit, Actions hijau). Tujuan user: Ramreader dan Trainer adalah MESIN yang dibagikan tanpa profil game; tujuan akhirnya trainer NARUTO standalone (belum didesain). Status: Langkah 1-8 terbukti jalan di NSUNSC. Langkah 9 (trainer): Paket A dan B terbukti. Paket C: kode terpasang, hotkey belum dites. Paket D (code patch): debugger Penulis Kode terbukti menangkap instruksi penulis. NOP polos terbukti membuat HP tidak turun TAPI musuh ikut kebal (dilaporkan user), dan chakra masih berkurang. Perbaikan 9De (code cave khusus pemain, bawaan semua patch; NOP polos hanya kalau Mode = Nop): TERBUKTI di NSUNSC, HP pemain tidak berkurang dan HP musuh tetap berkurang (dilaporkan user). Chakra: TERBUKTI di NSUNSC. Patch "Unlimited Chakra" sekarang di +94BA22 (movss [rcx+18h],xmm0, F3 0F 11 41 18 diganti cave pemain-saja); chakra tidak turun saat dinyalakan (dilaporkan user). Label yang sama menggantikan patch lama +94BC9B, yang memang pengisian. Lihat bagian 9, 11.10 dan 11.11 (11.11 = pekerjaan penanda sisi/ID karakter, yang sedang berjalan). Cooldown tagging (2c-1): alamat kanan (NSUNSC.exe+21BDC30 → +64) dan kiri (+21BDC30 → +60) tersimpan, penulisnya tertangkap di +7F3AB9 (kanan, 5 byte) dan +7F3964 (kiri, 4 byte); Freeze 100 di Trainer dites: karakter tetap tidak bisa ditag lagi (dilaporkan user), alamat ini kemungkinan hanya tampilan bar; patch belum dibuat.

## 0. Prompt pembuka untuk sesi baru

**BACA DULU (4 Nov 2026): bagian 2g (pekerjaan terakhir: durasi Awakening yang belum selesai) dan aturan 34. Bagian 2f dan 2g hanya mencatat sesi masing-masing; bagian lain tetap berlaku.**

Tempel ini bersama isi dokumen ini:

"Ini spec proyek Ramreader. Baca semua, terutama bagian 2 (cara kerja denganku; aturan 26 sampai 33 lahir dari kesalahan sesi AI sebelumnya), bagian 2f (serah terima terakhir) dan bagian 9 (status). Lanjutkan dari langkah pertama yang belum selesai di tabel bagian 9. Jangan membangun apa pun sebelum aku minta. Ulangi dulu aturan kerja versimu supaya aku tahu kamu paham."

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

26. **Yang dipakai user menentukan apa yang dihapus, bukan SPEC dan bukan tebakan AI.** Jangan menyuruh menghapus kartu, patch, atau fitur sebelum user bilang ia tidak memakainya dan hierarkinya sudah dicetak lewat perintah baca saja (aturan 27). Sesi 3 Nov 2026: AI menyuruh menghapus patch Unlimited Team Bar dan menganggap kartu KAWARIMI 2 boleh dihapus berdasarkan daftar lama di SPEC, padahal keduanya dipakai user lewat hotkey (F8 dan F9). Kartu Cooldown dihapus lebih dulu sehingga Cooldown Kanan (F5) hilang sebelum ada yang memastikan user tidak memakainya; profil harus dipulihkan dari cadangan. Yang "berfungsi" = yang user laporkan berfungsi.

27. **Patch tersimpan DI DALAM kartu alamat** (SavedAddress.CodePatches). Menghapus kartu menghapus patch di dalamnya dan toggle-nya dari Trainer. Sebelum menyuruh menghapus apa pun, cetak hierarkinya (hanya membaca): [regex]::Matches pada isi %AppData%\Ramreader\sessions\<game>.json dengan pola "Label": "..." diikuti "ValueType" (kartu) atau "Pattern" (patch). Hotkey patch disimpan terpisah di settings Trainer dan tetap tertulis walau patch sudah hilang. Mode patch (kosong, Team, Tree, Nop) ada di JSON sesi, bukan di settings.

28. **Ramreader dan Trainer adalah MESIN yang dibagikan ke banyak orang lewat GitHub Releases, TANPA profil game.** Profil NSUNSC (%AppData%\Ramreader\sessions\NSUNSC.json), settings Trainer, dan cadangan .bak milik user TIDAK BOLEH masuk zip rilis, repo, atau commit. Sesi 3 Nov 2026: AI membuat zip v0.1.1 berisi Profil\NSUNSC.json dan Pengaturan\NSUNSC.json karena salah baca tujuan user; user menghapusnya. Setelah membangun zip, buka isinya (System.IO.Compression.ZipFile.OpenRead) dan hitung berkas yang namanya mengandung Profil, Pengaturan, atau NSUNSC: harus 0. Trainer NARUTO adalah produk terpisah (2f), bukan bagian rilis mesin.

29. **Jangan menulis skrip yang mengubah file user sebelum melihat teks persisnya, dan jangan menimpa file yang belum dilihat.** Minta cuplikan lewat perintah baca atau Set-Clipboard dulu (aturan 14). Sesi 3 Nov 2026: AI menulis regex penghapus patch Team Bar tanpa pernah melihat bentuknya, dan membuat README baru lalu menyuruh menimpa README milik user tanpa pernah melihat isinya.

30. **Baca kata user secara literal, sesuai konteks pesan sebelumnya.** Salah baca 3 Nov 2026: "publish" berarti repo sudah dipublish ke GitHub (AI mengira dotnet publish); "dicek" berarti periksa berkas asli, bukan SPEC; "Normandy" ternyata Norland dan user kemudian menyuruh AI melupakannya (jangan dibahas lagi kecuali user mengangkatnya). Jangan bertanya hal yang jelas dari konteks (contoh bodoh: "game-nya sudah terpasang di PC-mu?").

31. **Teks perintah yang ditempel user tanpa output berarti perintah itu BELUM dijalankan.** Katakan itu dan minta outputnya; jangan menyimpulkan hasil. Kalau ada output, baca seluruhnya, termasuk baris peringatan dan hitungan.

32. **Ikon dan desain: jangan membuat "AI slop".** User menolak ikon buatan AI (chip RAM dengan huruf R atau T, latar gelap bergradien, aksen biru muda atau oranye) pada 3 Nov 2026 karena generik. Jangan membuat ulang ikon sebelum user menjelaskan gayanya (satu pertanyaan: warna, bentuk, atau gambar contoh). Hindari klise: persegi gelap membulat, gradien, huruf di dalam ikon, chip stok. Pemasangan ikon: satu baris ApplicationIcon di .csproj (belum pernah dipasang; kedua .csproj sudah dibaca).

33. **Kamu bukan satu-satunya sesi AI di proyek ini.** Perbarui hanya bagian SPEC yang kamu kerjakan. Jangan mengubah, menyatakan salah, atau menyatakan "belum dikerjakan" bagian yang bukan kerjamu: sesi lain atau user sendiri mungkin sudah mengerjakannya. Kalau menemukan teks lama yang berbahaya (contoh: 2e menyuruh menghapus kartu KAWARIMI 2), jangan menulis ulang barisnya; tulis koreksi di bagian milikmu dan sebut bagian mana yang digantikan. Sebelum mengedit SPEC, minta salinan terbaru dari user: SPEC milik user bisa sudah berbeda dari salinan yang AI pegang (3 Nov 2026 baris 2e nomor 2 sudah berbeda).

34. **Kalau tidak tahu fungsi sesuatu, bilang tidak tahu dan tanya; jangan menyuruh user mengetes hal yang fungsinya tidak AI ketahui.** 4 Nov 2026: AI menyuruh user menyalakan patch "Triger Awekening contoh" tanpa tahu fungsinya; user kesal. Pisahkan selalu: fakta (dari layar atau stopwatch user, atau hasil disassembler), dugaan (beri label), dan yang tidak diketahui (tulis "tidak tahu"). Jangan menyajikan tebakan sebagai langkah.

## 2b. Catatan serah terima 2 Nov 2026 (Langkah 9E, baca ini dulu)

**Terbukti (dilaporkan user dengan screenshot):**
- Pemindai Objek Karakter bekerja di instruksi apa pun yang menulis [reg+offset], bukan hanya chakra. HP = +94BAE2 (movss [rcx+10h], byte F3 0F 11 41 10). Kolom Instruksi harus berisi instruksi yang cocok dengan alamat tersimpan; bawaan lama 94BC94 (chakra) membuat HP seolah gagal. Kolom kini otomatis dari CodePatches bila dikosongkan (kode terpasang).
- Objek tertangkap hanya bila instruksi dipantau jalan: tiap karakter harus kena hit dulu.
- Penanda sisi = TIGA dword berurutan di objek karakter: +0x3C, +0x40, +0x44. Satu dword saja tidak cukup: di sesi PID 27268 +0x44 sama untuk kedua sisi sehingga musuh dikira tim. Offset yang membedakan berbeda antar sesi. Nilainya berganti antar sesi dan berasal dari kumpulan kecil, jadi JANGAN disimpan di profil; Trainer membacanya dari objek pemain tiap tick.
- Tombol Tandai Otomatis (Objek Karakter) menandai Tim/Musuh dengan membandingkan 12 byte itu dengan objek "Tim (aku)". Jangan menyebut tanda manual user "tidak terverifikasi"; user tersinggung dan Tandai Otomatis sudah menjawabnya.
- CodeCave Mode Team (CodeCave.BuildTeamDiagMulti, PatchItem.ComputeMarkers, Mode "Team" di JSON sesi) melindungi seluruh tim DAN musuh tetap kena damage, untuk God Mode (HP). Dites user di 3 kondisi (sesi sama, rematch, karakter baru): stabil. Diagnostik di kartu: dilewati 90x, dijalankan 52x. KETERBATASAN (dilaporkan user, screenshot 2 Nov 2026, PID 27480): penanda 12 byte mengikuti KARAKTER UTAMA tim, bukan sisi. Bila karakter utama kedua sisi sama (mirror, mis. Sakura vs Sakura), keempat objek tertangkap bertanda identik A2128926 80DB7929 D70455F6, sehingga musuh dan sub-karakter musuh dibaca Tim walau sub-karakternya berbeda (Tandai Otomatis: 4 Tim, 0 Musuh). Bila karakter utama berbeda (sub sama pun), penanda beda dan musuh terbaca Musuh. Mode Team BELUM mendukung mirror; perlu field tambahan pembeda sisi (belum ditemukan; dugaan: penunjuk objek tim atau indeks slot). Hasil tes (screenshot user, PID 27480, mirror, tanda manual 3 Tim dan 3 Musuh, konsisten dengan pasangan HP kembar): Bandingkan ronde 1 memberi 0 kandidat. Tidak ada dword di 0x800 byte pertama objek yang seragam di Tim dan beda dari semua Musuh. Jadi penanda sisi di mirror bukan dword biasa di objek karakter. Dugaan belum terbukti: ada di luar 0x800 byte, atau sisi disimpan di objek tim yang menunjuk karakter. Usul: pencarian terbalik (objek yang menunjuk basis Tim), lalu cave membandingkan dengan beberapa slot. Sementara itu, mirror memakai Mode pemain-saja. Tes ulang (PID 16052, game dibuka ulang, mirror lagi, dilaporkan user dengan screenshot): Tandai Otomatis 6 Tim 0 Musuh, keenam objek bertanda A2128926 80DB7929 D70455F6. Bandingkan ronde 1: 1 kandidat +0x10 (Tim 0x42C464DE = 98,2; Musuh 0x42B4B680 = 90,36 dan 0x42B6FFA4 = 91,5) = HP float, lolos karena HP Tim kebetulan sama; ronde 2: 0 kandidat. Dua sesi game berbeda, hasil sama: tidak ada penanda sisi berupa dword di 0x800 byte pertama objek. 9E-a1 (kode ditulis, belum dikompilasi, belum dites): Core\Debugging\ReversePointerFinder.cs, pencarian terbalik penunjuk ke objek karakter (jendela basis -0x40..+0x40, dugaan: awal objek = rcx-0x28 dari hitungan jalur HP/chakra/gauge). 9E-a1: build lolos (dilaporkan user, 2 Nov 2026). 9E-a2 (kode terpasang, belum dites): tombol Cari Penampung di Objek Karakter memindai memori game, laporan [A] (penunjuk di dalam objek) dan [B] (penampung luar, TIM-SAJA/MUSUH-SAJA/CAMPUR) disalin ke clipboard. Syarat: objek sudah ditandai (di mirror semua otomatis Tim; pilih baris musuh lalu Tandai Musuh) dan Berhenti dulu. HASIL 9E-a2 (laporan user, PID 16052, mirror, 3 Tim dan 3 Musuh, laporan disalin ke clipboard): semua penunjuk menuju basis -0x28 (awal objek = rcx-0x28). Di awal tiap objek ada tiga penunjuk (+0x00, +0x08, +0x10): Tim T2 (+0x00 ke A, +0x10 ke T1, A dan T1 +0x08 ke T2), Musuh M1 (+0x00 ke M2, +0x10 ke M3, M2 dan M3 +0x08 ke M1). Penampung luar @0x1A000189120: Tim +0x00=A +0x08=T2 +0x10=T1; Musuh +0x80=M2 +0x88=M1 +0x90=M3. Dugaan belum terbukti: pohon merah-hitam (std::map MSVC), satu pohon per sisi, kepala berjarak 0x80. Pengelompokan dari memori sama persis dengan tanda user. Sisi di mirror ditentukan oleh pohon, bukan nilai karakter. Rencana: Mode "Tree" (Trainer menelusuri +0x00/+0x08/+0x10 dari objek pemain tiap tick, mengisi beberapa slot di cave). 9E-b (kode ditulis, belum dikompilasi, belum dites): Core\Debugging\SideTree.cs (Collect: telusuri +0x00/+0x08/+0x10 dari awal simpul pemain = rcx-0x28, hanya sambungan timbal balik, maksimal 12 simpul), CodeCave.BuildTreeDiag (cmp reg dengan 12 slot, je rel32, blok data diagnostik sama dengan Multi), PatchItem Mode "Tree" (ComputeTreeSlots, _lastTree, Tick menulis slot saat berubah). Ubah Mode ke "Tree" lewat penggantian teks di JSON (God Mode dan Unlimited Chakra). TERBUKTI di NSUNSC (dilaporkan user, screenshot kartu Trainer, PID 16052, 2 Nov 2026): mirror 2x (karakter utama sama, termasuk tag ke sub-karakter) dan 1x dengan karakter utama berbeda; seluruh sisi pemain kebal untuk HP (God Mode) dan chakra (Unlimited Chakra), musuh tetap kena. Diagnostik kartu: God Mode dilewati 19x dijalankan 74x; Unlimited Chakra dilewati 89x dijalankan 81x. Aturan 25 terpenuhi. Mode Tree menggantikan Mode Team untuk mirror; Mode Team tetap ada. Team Tag (+94BB7A) masih Mode kosong; tes Mode Tree untuk Team Tag lewat JSON dikerjakan berikutnya (dugaan: rax-0x28 juga simpul pohon).

**Belum dikerjakan / belum terbukti:**
- Unlimited Chakra (+94BA22) masih mode pemain-saja: dites 8x (dilaporkan user), chakra karakter utama tidak turun tapi sub-karakter/tim TIDAK unlimited. Mode Team untuk chakra belum dicoba (ubah Mode lewat penggantian teks di JSON, lihat aturan 23); dugaan belum terbukti: objek chakra ([rcx+18h]) dan HP ([rcx+10h]) satu objek, jadi penanda 3 dword sama berlaku. Summon/Team Tag (+94BB7A, gauge, [rax+2Ch]): dilaporkan user 8x cooldown summon/Tag Team tidak terpengaruh; alamat yang dipatch adalah gauge milik TIM, bukan karakter utama. Perlu cari alamat gauge karakter utama lewat Cari Nilai, Jalur Stabil, Penulis Kode, lalu cek apakah register basisnya objek karakter (punya penanda 12 byte).
- MASALAH COM (laporan user, 2 Nov 2026): maksudnya P1 vs COM, musuh COM ikut kebal (PvP lawan P2 sudah benar). Setelah itu user menguji 8 kali (rematch, keluar ke menu/pilih karakter, masuk lagi): musuh COM kena damage, P1 dan tim kebal, konsisten; Team/Tag Team berfungsi. Penyebab kegagalan sebelumnya TIDAK diketahui (dugaan belum terbukti: cave lama tertinggal di game akibat Trainer ditutup paksa). Anggap selesai kecuali muncul lagi.
- Tombol Ulangi Irisan: user melaporkan "tidak bisa direset"; penyebab di kode tidak ditemukan (logika benar). Usulan pesan konfirmasi di kotak Info mungkin belum dipasang.
- Dugaan: menutup Ramreader paksa saat Mulai Pantau menyebabkan game crash (breakpoint DR0 tertinggal). Suruh user klik Berhenti dulu. Menutup Trainer paksa saat patch menyala meninggalkan lompatan ke cave lama; matikan toggle dulu.
- Kode Mode Team dan Tandai Otomatis mungkin belum di-commit; cek git status.

**Preferensi user (kasar bila kesal, tapi alasannya sah):** jawaban pendek, hasil dulu; selalu beri perintah build+buka Ramreader dan Trainer sekaligus (aturan 22); user sedang menghemat token; user tidak mau membaca memori sendiri atau membuka CE; otomatiskan semua yang bisa; jangan mengajukan dugaan sebagai kepastian.

## 2c. Catatan serah terima 2 Nov 2026 (akhir sesi, Langkah 9E, BACA INI PERTAMA)

Bagian ini menggantikan teks lama yang bertentangan (bagian 2b, tabel 9E, 11.11 bagian "Yang BELUM terbukti"). Jangan membangun apa pun sebelum user minta (aturan 1).

**Terbukti (dilaporkan user dari layarnya, NSUNSC, PID 16052).**
- God Mode (HP, +94BAE2, [rcx+10h]) dan Unlimited Chakra (+94BA22, [rcx+18h]) berlaku untuk SELURUH sisi user dan tidak untuk musuh, di Mode "Tree". Dites: mirror 2x (karakter utama sama kedua sisi), 1x karakter utama berbeda. Ganti karakter lewat TAG di match yang sama: HP dan chakra tetap aman. (Tagging = ganti karakter lewat tag di match yang sama, bukan match baru. Jangan salah baca.) Diagnostik kartu: God Mode dilewati 19x dijalankan 74x; Chakra dilewati 89x dijalankan 81x.
- Cooldown SUMMONING (Team Tag, +94BB7A, movss [rax+2Ch],xmm0) berhasil, termasuk saat karakter utama menjadi sub dan sub lain yang aktif. Diasumsikan Mode Tree diterapkan lewat JSON sesuai perintah AI (belum dikonfirmasi eksplisit).
- Masalah COM lama (musuh COM ikut kebal) hilang di 8 uji, tapi penyebab sebenarnya baru ketemu belakangan: penanda sisi lama (3 dword di +0x3C/+0x40/+0x44) mengikuti KARAKTER UTAMA tim, bukan sisi. Bila karakter utama kedua sisi sama (mirror), semua objek bertanda identik dan musuh dianggap Tim. Mode "Team" (penanda) tidak bisa menyelesaikan mirror. Bandingkan 0x800 byte objek memberi 0 kandidat di mirror (2 sesi: PID 27480 dan 16052).

**Temuan struktur (kunci).**
- Awal objek karakter = nilai register basis dikurangi 0x28 (rcx-0x28 untuk HP dan chakra, rax-0x28 untuk gauge Team Tag). Dugaan: basis adalah komponen di +0x28 dalam objek.
- Di awal tiap objek ada TIGA penunjuk: +0x00, +0x08, +0x10, ke karakter satu sisi, timbal balik (pohon, dugaan std::map MSVC). Tim: akar T2 (+0x00 ke A, +0x10 ke T1; A dan T1 +0x08 ke T2). Musuh serupa. Penampung luar (kepala pohon) berisi dua kepala berjarak 0x80. Pengelompokan dari memori sama persis dengan tanda manual user.
- Cari Penampung (alat) menemukan ini: pencarian terbalik penunjuk ke objek karakter di seluruh memori writable.

**Kode yang ada (build lolos, Mode Tree terbukti).**
- Core\Debugging\SideTree.cs: Collect() menelusuri tiga penunjuk dari simpul pemain, hanya sambungan timbal balik, maksimal 12 simpul. BaseFromNode = 0x28 (di-hardcode: objek yang basisnya bukan 0x28 dari simpul perlu penyesuaian).
- Core\Debugging\CodeCave.cs: Build (pemain-saja, 1 slot), BuildTeamDiagMulti (penanda 12 byte), BuildTreeDiag (12 slot, je rel32, blok diagnostik 24 byte).
- Core\Debugging\ReversePointerFinder.cs: Scan() dan Report() (bagian [A] penunjuk di dalam objek, [B] penampung luar TIM-SAJA/MUSUH-SAJA/CAMPUR).
- Trainer\ViewModels\PatchItem.cs: Mode kosong = pemain-saja; "Team" = penanda 12 byte; "Tree" = pohon; "Nop" = NOP polos (JANGAN disuruh, aturan 20). Tick menulis slot tiap tick saat berubah.
- Ramreader.App: halaman Objek Karakter punya tombol Cari Penampung (menyalin laporan ke clipboard, tempel ke chat tanpa screenshot).
- Cara mengganti Mode di JSON sesi (%AppData%\Ramreader\sessions\NSUNSC.json): regex pada Label patch, cadangan .bak, cetak jumlah penggantian, JANGAN ConvertFrom/To-Json (aturan 23). Ramreader.App dan Trainer ditutup dulu.

**Alamat penting (NSUNSC.exe+offset).** HP writer 94BAE2; chakra writer 94BA22 (94BC94 dan 94BC9B = pengisian, jangan disentuh); gauge Team Tag writer 94BB7A, pengisi 94BDEF, batas atas 100 dipaksa 94BDF6. Jalur HP berujung "250,0,38", chakra "250,0,40", Team Tag "740,10,54" (semua dari akar 21D74xx).

**KERJA BERIKUTNYA (user yang mencari alamat; AI menuntun langkah demi langkah).**
1. Cooldown TAGGING: SELESAI untuk satu kondisi (dilaporkan user, 3 Nov 2026): patch Tag Switch di +94C3B6, Mode Tree. Rincian, bukti, dan jalan buntu ada di bagian 2d. Alamat ditemukan (dilaporkan user, screenshot 2 Nov 2026): bar naik sampai 100. Cooldown (kanan, analog kanan ke kanan) = NSUNSC.exe+21BDC30 → +64, terverifikasi setelah restart; Cooldown2 (kiri) = +21BDC30 → +60, 3 jalur lolos ronde 1. Penulis di Penulis Kode: kanan +7F3AB9 movss [rdi+4],xmm0 (hit 753, turun 3); kiri +7F3964 movss [rdi],xmm0 (hit 502, turun 2; sesi kedua hit 696, turun 3); tiga baris per sisi = satu instruksi (dugaan, selisih 1 byte). Kiri hanya 4 byte, belum memenuhi syarat cave 5 byte (dugaan dari enkoding). Freeze 100 di Trainer dites: karakter tetap tidak bisa ditag lagi (dilaporkan user), jadi Freeze tidak cukup. Dugaan belum terbukti: alamat ini hanya tampilan bar, penghitung asli ada di tempat lain. Belum dites: patch cave kanan.
2. Bar TEAM oranye (untuk combo tim). SELESAI lewat Kunci 100 di Trainer, tanpa patch (dilaporkan user, 3 Nov 2026), lihat bagian 2d.
3. Kawarimi (berubah jadi batang pohon). Belum dicari; tanyakan user dulu seperti apa tampilannya di HUD (bar, hitungan, atau timer), satu pertanyaan saja.
Alur baku, tanpa Cheat Engine: (a) Cari Nilai: tipe float (gauge di game ini float 0-100), pakai Snapshot nilai awal tidak diketahui lalu Scan Lanjutan (Berkurang/Bertambah/Tidak berubah) selagi user memakai fitur dan menunggu cooldown; (b) Jadikan Target, Jalur Stabil (cari jalur, restart game, verifikasi), Simpan Alamat; (c) Penulis Kode (debugger) menangkap instruksi penulis, pilih baris movss terpanjang; (d) Buat Patch; (e) cek register basis dan displacement di Objek Karakter: kalau objeknya simpul pohon, Mode Tree; kalau objeknya satu per sisi (mis. objek tim bersama, dugaan belum terbukti untuk bar oranye), Mode kosong (pemain-saja) sudah cukup karena hanya satu objek per sisi; kalau perlu, Cari Penampung memberi struktur objek barunya. (f) Tes 3 kondisi (aturan 25), lalu catat.

**Pelajaran sesi ini (tambahan aturan).**
- Info di Ramreader tidak bisa disalin: pakai tombol yang menyalin ke clipboard (Cari Penampung) atau Set-Clipboard, jangan minta screenshot berulang (aturan 24).
- Semua edit SPEC dengan Sebelum/Sesudah pada teks SATU BARIS yang diverifikasi persis ada (cek dulu dengan grep di salinan file bila ada).
- Hindari anggapan: tanyakan atau baca ulang maksud user ("tagging", "COM", "mirror") sebelum merancang tes; salah baca sudah memicu frustrasi.
- Tes baku: aturan 22 (satu perintah tutup, build, buka App dan Trainer). Klik Berhenti di Ramreader dan matikan toggle Trainer sebelum menutup proses, jangan paksa.

**Commit.** Kode 9E (SideTree, BuildTreeDiag, ReversePointerFinder, Mode Tree, Cari Penampung) mungkin belum di-commit: cek git status.

## 2d. Catatan serah terima 3 Nov 2026 (Langkah 9E, Unlimited Tag Switch). BACA INI SETELAH 2c

Menambah 2c. Teks 2c nomor 1 (cooldown tagging "belum dicari") sudah usang.

**Terbukti (dilaporkan user dari layarnya, NSUNSC PID 26956).**
- Patch "Tag Switch" di NSUNSC.exe+94C3B6 (movss [rax+2Ch],xmm6), Mode "Tree", pemilik alamat Team Tag: tag kanan dan kiri bisa dipakai berulang tanpa cooldown. Dites 3 match dengan semua cheat menyala, semuanya berjalan (dilaporkan user, 3 Nov 2026); kondisi rematch dan ganti karakter tidak dirinci user. Belum dicek apakah COM tetap kena cooldown tag (itu bukti bahwa patch hanya untuk sisi user).
- Penulis ditemukan lewat Penulis Kode pada alamat Team Tag, tanpa summoning, 3 kali tag: +94C3B6 Hit 5, Berubah 3, Turun 3 (jumlah tag = 3). Baris adc +94C3B8 dan movups +94C3B7 hanya potongan byte yang sama, pilih movss.
- Penulis lain pada objek yang sama, JANGAN disentuh: +94BDEF (pengisi, Turun 0, 729 hit) dan +94BDF6 (batas 100, Turun 2). +94BB7A = penurun summon (Team Tag, sudah terbukti).
- Hex objek karakter (awal 1DE5BF90F10 = rcx 1DE5BF90F38 dikurangi 0x28): F48 dan F50 sesuai [rcx+10h] dan [rcx+18h] (HP dan chakra). F64 = awal+0x54 = [rax+2Ch] dengan rax = awal+0x28; nilainya loncat-loncat saat summon (28.4, 2.8, sekitar 61.2). Dugaan belum terbukti: F64 adalah gauge pendukung yang dikonsumsi summon DAN tag, lewat dua instruksi berbeda (+94BB7A untuk summon, +94C3B6 untuk tag).

**Jalan buntu (jangan diulang).**
- Alamat Cooldown (NSUNSC.exe+21BDC30 -> +64 = kanan, +60 = kiri, float, terbaca 100, turun saat tag lalu naik ke 100). Penulisnya movss [rdi+4],xmm0 di +7F3AB9 (669 hit, Turun 3) dan movss [rdi],xmm0 di +7F3964 (375 hit, Turun 2). Patch di kedua instruksi itu terpasang dan toggle menyala, tetapi cooldown tetap ada (dilaporkan user). Jangan dipatch lagi.
- Alasan (bacaan kode dari Cheat Engine, dugaan): di +7F3AB9 game memanggil fungsi di +94B4B0, yang mengembalikan [rax+2Ch] dari satu entri (indeks edx+1) atau 0 kalau entri tidak ada; hasil dibandingkan dengan [rdi+4] (ucomiss), ditulis ke [rdi+4], lalu dikirim ke tampilan lewat SetGaugeRateR untuk "supportGauge". Jadi +60/+64 = salinan tampilan, bukan penentu cooldown.
- Scan ulang saat cooldown tagging jalan (Snapshot lalu Berubah) menghasilkan 3 kandidat float; user memilih yang bernilai 100 (0x1DE5BAB7784). Jalur teratasnya NSUNSC.exe+21BDC30 -> +64, SAMA dengan kartu Cooldown lama, dan penulis yang tertangkap sama (+7F3AB9). Jadi dua scan menunjuk salinan tampilan yang sama.
- Hitungan alamat (sesi PID 26956): AllocationBase NSUNSC.exe = 7FF64E990000. 7FF64F183AB9 dikurangi basis = +7F3AB9; 7FF64F2DB4B0 dikurangi basis = +94B4B0.

**Kode yang ditambah sesi ini (build lolos, dilaporkan user).**
- CodePatch.Group dan PatchItem (patch ber-Group sama menyala/mati bersamaan lewat Registry): belum terbukti. User melaporkan kedua toggle Cooldown bisa menyala, tetapi tidak melaporkan bahwa satu toggle menyalakan keduanya. Skrip pemasang Group sempat menghasilkan DUA baris "Group" di JSON (satu "Cooldown", satu "" bawaan); skrip perbaikan tidak pernah dilaporkan hasilnya. Cek JSON sebelum memakai Group.
- CodeCave.TryAnalyze menerima penulis kurang dari 5 byte (CaveInfo.WriterLength, CodeCave.BuildTail, TailOffset 0x800), dan CodePatchMaker menggabungkan instruksi sesudahnya. Terbukti membuat dan memasang patch Kiri 8 byte (F3 0F 11 07 F3 0F 5E C7); toggle menyala (dilaporkan user). Tidak berguna untuk cooldown (lihat jalan buntu).
- PatchItem: pesan error toggle bertahan 20 detik (sebelumnya langsung tertimpa "Siap. Patch belum aktif.").
- Pembersihan yang tertunda: kartu Cooldown dan Cooldown2.2 (dan patch Kanan/Kiri di dalamnya) boleh dihapus di Tersimpan. Cooldown2 sudah dihapus atau tidak dibutuhkan (tanpa patch).

**Bar TEAM oranye (2c nomor 2): BERHASIL lewat Kunci 100.** Dilaporkan user (3 Nov 2026): nilai Team Bar diset 100 dan dikunci di Trainer, bar langsung penuh dan tidak berkurang, dites di DUA pertandingan berbeda. Kunci otomatis (dilaporkan user, 3 Nov 2026): Preset 100 dan AutoFreeze true di settings Trainer (%AppData%\Ramreader.Trainer\settings\NSUNSC.json, kartu Team Bar); bar langsung penuh, tidak reset ke 0 setelah game direstart, dan tetap penuh saat dipakai. Kode: FeatureSettings.AutoFreeze, FeatureItem.TryAutoFreeze (mengunci sekali per proses game kalau Preset terisi). AutoFreeze tanpa Preset tidak melakukan apa-apa (terbukti gagal, dilaporkan user). Patch anti-turun dari +A337CA (mov [rbx+rax*8+74h],ebp, satu-satunya baris Turun) TIDAK berguna: bar mulai dari 0, jadi yang dibutuhkan pengisian, bukan anti-turun; jangan diulang. Belum diverifikasi: sisi COM. Penulis lain ditangkap di sesi akhir: +94BE29 (Hit 50, Turun 0), +94BE30 (Hit 1, Turun 1), +A3249F (Hit 1); penurun pemakaian bar tidak pernah tertangkap dan tidak dibutuhkan karena Kunci sudah cukup. Riwayat pencarian: Alur: Snapshot float saat bar diam, lalu Scan Lanjutan Berubah, lalu Bertambah setelah bar naik karena serangan. Hasil (dilaporkan user, screenshot): 9 kandidat; yang dipilih user adalah dua alamat bernilai 78.46841 (sebelumnya 70.88947): 0x1DE545D73D4 dan 0x1DE58BC9AA0. Alamat 0x1DE545D73D4 sudah dijadikan target dan disimpan (label Team Bar, 99 jalur, belum diverifikasi restart). Penulis (dilaporkan user, screenshot Penulis Kode, 52 hit): +94BE29 movss [rcx+5Ch],xmm0 (Hit 50, Turun 0; dugaan: pengisi); +94BE30 mov dword [rcx+5Ch],42C80000h (Hit 1, Turun 1; 42C80000 = 100.0, dugaan: pemaksa batas atas, bukan penurun); +A3249F mov dword [r14+5Ch],42C80000h (Hit 1, Berubah 0; dugaan: pengisian awal). Penurun (pemakaian bar) BELUM tertangkap; tidak diketahui apakah user memakai bar saat memantau. Jangan patch +94BE29 dan +94BE30. Alamat mentah milik sesi game itu, jangan disimpan sebagai tetap. Jalur Team Bar bekerja setelah restart game (dilaporkan user); VerifiedRounds di sesi masih 0.

**Kerja berikutnya.** (1) Bar TEAM: SELESAI lewat Kunci 100 otomatis (Preset 100 + AutoFreeze). (2) Tes 3 kondisi Tag Switch: SUDAH (3 match, dilaporkan user). (3) Kawarimi (tanyakan user dulu tampilannya di HUD). (4) Commit kode 9E dan sesi ini (cek git status).

## 2e. Catatan serah terima 3 Nov 2026 (akhir sesi, Team Bar dan Kawarimi). BACA INI SETELAH 2d

Menggantikan 2d untuk bar TEAM dan menambah Kawarimi. Header "Terakhir diperbarui" dan tabel bagian 9 sudah diperbarui (3 Nov 2026, akhir sesi).

**Terbukti (dilaporkan user dari layarnya, NSUNSC, PID 20432).**
- Tag Switch (+94C3B6, Mode Tree): dites 3 match dengan semua cheat menyala, semuanya berjalan. Restart PC untuk Tag Switch belum dites. Cheat lain selain Tag Switch dan Team Bar tetap berfungsi setelah restart PC.
- Bar TEAM: SELESAI lewat Kunci 100 otomatis. Di %AppData%\Ramreader.Trainer\settings\NSUNSC.json, entri fitur "Team Bar" berisi Preset "100" dan AutoFreeze true (kode: FeatureSettings.AutoFreeze, FeatureItem.TryAutoFreeze, mengunci sekali per proses game kalau Preset terisi; AutoFreeze tanpa Preset tidak melakukan apa-apa, terbukti gagal). Bar langsung penuh, tidak reset ke 0 setelah game direstart, tetap penuh saat dipakai. Patch anti-turun dari +A337CA (mov [rbx+rax*8+74h],ebp) TIDAK berguna karena bar mulai dari 0; jangan diulang. Edit CodeCave.BuildIndexed (dukungan register indeks) sempat diberikan tetapi tidak dipakai; cek apakah terpasang di file sebelum percaya.
- Kawarimi (HUD: kapsul oranye di sebelah ikon karakter; nilai float, penuh = 100, turun 100, 75, 50 per pakai, dilaporkan user). Alamat dicari lewat Cari Nilai: float, Scan Pertama nilai 100 saat penuh, pakai Kawarimi, Scan Lanjutan Nilai berkurang. Hasil: dua alamat kembar 0x269EEF3E2CC dan 0x269EEF4F318 (nilai 50, sebelumnya 100; alamat mentah milik sesi PID 20432). Target 0x269EEF4F318, Cari Jalur, Simpan Alamat label "KAWARIMI 2". Kunci manual 100 di Trainer terbukti: bar tidak berkurang saat dipakai. Preset 100 dan AutoFreeze untuk KAWARIMI 2 sempat dites dan otomatis 100 di satu sesi; sesudah itu Preset dikosongkan dan AutoFreeze dimatikan lewat skrip regex supaya patch bisa dites sendiri.
- Penulis Kode pada KAWARIMI 2 (jendela dimaksimalkan, 171 hit): hanya satu instruksi, movss [rcx+20h],xmm2 di NSUNSC.exe+94C09E (Hit 171, Berubah 171, Turun 4 = 4 kali pakai). Baris adc +94C0A0 dan movups +94C09F potongan byte yang sama, jangan dipilih. Buat Patch pertama GAGAL ("Tidak ada pola yang unik"). Perbaikan: CodePatchMaker.cs, Pre = { 0, 4, 8, 12, 16, 24, 32 } dan Post = { 4, 8, 12, 16, 24, 32, 48, 64 }. Setelah build, Buat Patch berhasil (dilaporkan user), patch "Unlimited Kawarimi" di Trainer: bar Kawarimi TIDAK berkurang saat dipakai. Mode kosong (terbaca dari JSON sesi, 3 Nov 2026): karakter utama terlindungi, sub-karakter tim TIDAK (dilaporkan user). Mode diubah ke Tree lewat skrip regex (1 kecocokan, 1 penggantian, cadangan NSUNSC.json.20261103-101615.bak, baris Mode Tree tercetak). Sesudah itu user melapor trainer berjalan, tanpa rincian per kondisi.

**Belum dirinci (jangan dicatat terbukti).** Patch Unlimited Kawarimi dengan Mode Tree: user belum melaporkan terpisah apakah sub-karakter terlindungi lewat tag, rematch, dan restart game. Aturan 25 belum terpenuhi untuk Kawarimi. Dugaan, belum terbukti: +94C09E juga pengisi pelan, sehingga bar membeku di nilai saat patch dinyalakan (bar harus 100 dulu).

**Jalan buntu (jangan diulang).** Alamat 0x269E927E02C (hasil Snapshot, label "Kawarimi"): SALAH. Set 100 tidak mengubah angka di kartu maupun kapsul. Jangan dipakai. Scan Pertama dengan nilai yang diketahui user lebih akurat daripada Snapshot untuk bar yang punya nilai bulat (100, 75, 50).

**KERJA BERIKUTNYA (urut).**
1. Tes tiga kondisi untuk Unlimited Kawarimi dengan Mode Tree, dilaporkan terpisah per kondisi (rematch, tag ke sub-karakter, restart game), lalu catat.
2. Bersih-bersih di Ramreader, menu Tersimpan: hapus kartu "Kawarimi" yang SALAH, kartu "KAWARIMI 2" (Kunci, sudah digantikan patch), Cooldown, Cooldown2.2 beserta patch Kanan/Kiri, (anti-turun tidak berguna). Entri "KAWARIMI 2" di settings Trainer boleh dibiarkan. Unlimited Team Bar berfungsi.
3. Perbarui header, tabel bagian 9, dan 2e dengan hasil tes (Sebelum/Sesudah yang diverifikasi persis, aturan 13 dan 21).
4. git status, lalu commit (tidak menyuruh push). SUDAH: kode 9E dan README sudah di GitHub (dilaporkan user, screenshot 3 Nov 2026); perbaikan workflow ke Ramreader.slnx commit 09c21ea, Actions #10 hijau.

**Pelajaran sesi ini.** Ambil Snapshot adalah tombol di sebelah kanan Scan Pertama (kotak Nilai dibiarkan kosong); Scan Pertama selalu butuh angka. Jangan menyuruh Simpan Alamat sebelum alamatnya terbukti berubah kedua kali. Jangan menebak jenis nilai (int atau float) dari tampilan HUD; tanyakan atau pakai Snapshot lalu Berkurang. Baca nilai yang user sebut secara literal (100, 75, 50). Satu perintah PowerShell lengkap, jangan merujuk pesan lama.

## 2f. Catatan serah terima 3 Nov 2026 (sesi rilis dan bersih-bersih). BACA INI SETELAH 2e

Hanya mencatat pekerjaan SATU sesi AI (aturan 33). Hal yang tidak tertulis di sini belum tentu belum dikerjakan: sesi lain atau user mungkin sudah mengerjakannya. 2c sampai 2e tetap berlaku kecuali bagian ini menyebut menggantikannya.

**Fakta dari output perintah atau screenshot user (kecuali berlabel dugaan).**
- Kawarimi: di JSON sesi, patch "Unlimited Kawarimi" bermode kosong. User melapor karakter utama terlindungi dan sub-karakter tim TIDAK. Mode diganti ke "Tree" lewat [regex]::Replace (1 kecocokan, 1 penggantian; cadangan %AppData%\Ramreader\sessions\NSUNSC.json.20261103-101615.bak). Sesudah itu user melapor "trainernya berjalan". Screenshot Trainer: patch aktif dalam mode pohon, diagnostik dilewati 60x dijalankan 6x. Tes tiga kondisi (rematch, tag ke sub-karakter, restart game) TIDAK dirinci user di sesi ini, jadi jangan dicatat terbukti dari sesi ini.
- Hierarki Tersimpan (hasil perintah baca): patch tinggal di dalam kartu: HP (God Mode), Chakra (Unlimited Chakra), Team Tag (Team Tag dan Tag Switch), Cooldown (Cooldown Kanan), Cooldown2.2 (Cooldown Kiri), Cooldown Tag (tanpa patch), Team Bar (Unlimited Team Bar), KAWARIMI 2 (Unlimited Kawarimi), Gold Ticket (tanpa patch). Hotkey di settings Trainer: God Mode F2, Unlimited Chakra F3, Team Tag F4, Cooldown Kanan F5, Cooldown Kiri F6, Tag Switch F7, Unlimited Team Bar F8, Unlimited Kawarimi F9.
- Isi patch Team Tag dan Tag Switch di JSON: Pattern, InstructionOffset, OriginalBytes, PatchBytes, Note, Mode "Tree", MarkerOffset 68; tidak ada rujukan ke kartu lain (Team Tag: F3 0F 11 40 2C; Tag Switch: InstructionOffset 8, F3 0F 11 70 2C). Kartu Cooldown Tag tidak dipakai keduanya.
- User melaporkan yang berfungsi dan dipakai: God Mode, Unlimited Chakra, Team Tag, Tag Switch, Unlimited Team Bar, Unlimited Kawarimi. Cooldown Kanan dan Kiri tidak dipakai.
- Bersih-bersih: user menghapus lewat tombol Hapus kartu "Kawarimi" (yang salah), Cooldown, Cooldown2.2, dan Cooldown Tag. Screenshot sesudahnya: 6 alamat tersimpan (Gold Ticket, HP, Chakra, Team Tag, Team Bar, KAWARIMI 2). Patch Cooldown Kanan dan Kiri ikut hilang bersama kartunya (dugaan kuat dari hierarki; daftar patch sesudah penghapusan belum dicetak). F5 dan F6 masih tertulis di settings Trainer.
- **Menggantikan 2e KERJA BERIKUTNYA nomor 2:** kartu "KAWARIMI 2" dan patch "Unlimited Team Bar" TIDAK dihapus (dipakai user). Cooldown, Cooldown2.2, dan Cooldown Tag sudah dihapus.
- Insiden: kartu Cooldown dihapus sebelum AI memastikan F5 tidak dipakai; profil dipulihkan dari D:\Projects\Ramreader-dist\NSUNSC.json.20261103-103922.bak (11868 karakter; sesudah disalin, Compare-Object terhadap cadangan 0 baris beda), lalu user menghapus ketiga kartu itu sendiri.
- Build solusi Release lokal lolos (0 error; 2 warning CS0219 "markerOffset" di CharacterObjectsViewModel.cs baris 296, tidak berbahaya). Berkas solusi bernama Ramreader.slnx, BUKAN .sln (bagian 5 salah). Workflow .github\workflows\build.yml memanggil Ramreader.sln (error MSB1009), diganti ke .slnx, commit 09c21ea, Actions run #10 hijau.
- README: README.md (Indonesia) dan README.en.md (English), saling tertaut di baris Language. GitHub sempat menampilkan teks Inggris di README.md tanpa README.en.md; dibetulkan (diagram mermaid dipecah per baris).
- SPEC: 7 penggantian satu baris (commit e23f6c3), lalu bagian 2f dan aturan 26 sampai 33 ini.

**Rilis (fakta).** Zip dibuat di luar repo (D:\Projects\Ramreader-dist): Stop-Process Ramreader.App dan Ramreader.Trainer; dotnet publish src\Ramreader.App -c Release -r win-x64 --self-contained true -o <folder>; dotnet publish src\Ramreader.Trainer (opsi dan folder sama); Compress-Archive; cek isi zip dengan ZipFile.OpenRead. Ramreader-v0.1.0-win-x64.zip = 63,3 MB, 413 berkas, tanpa Profil, Pengaturan, atau .json milik user; di GitHub Releases sebagai "Ramreader v0.1.0 (beta)" (screenshot). Pengguna harus mengekstrak seluruh zip dan menjalankan sebagai Administrator; berkas belum ditandatangani (SmartScreen kemungkinan memperingatkan: dugaan). Kejadian: skrip v0.1.1 pertama memasukkan Profil\NSUNSC.json dan Pengaturan\NSUNSC.json ke zip (415 berkas); user menghapus zip dan folder v0.1.1, tidak pernah diunggah. v0.1.1 dibangun ulang bersih: 413 berkas, 0 berkas profil atau pengaturan (screenshot user), D:\Projects\Ramreader-dist\Ramreader-v0.1.1-win-x64.zip, BELUM diunggah ke Releases. Dugaan: isinya sama dengan v0.1.0 (jumlah berkas sama; diff commit belum dibuka). Cadangan di D:\Projects\Ramreader-dist: NSUNSC.json.20261103-103922.bak (profil sebelum bersih-bersih), NSUNSC.json.sebelum-v0.1.1.bak (profil sesudah bersih-bersih, 9217 byte), SPEC.md.20261103-103310.bak. Trainer hanya memuat *.json di %AppData%\Ramreader\sessions, jadi berkas .bak di sana tidak terbaca.

**Kode yang dibaca (tidak diubah) dan temuannya.** ProfileStore: Trainer memuat %AppData%\Ramreader\sessions\*.json (array SavedAddress) dan berkas ekspor GameProfile; nama profil = nama berkas untuk array. SettingsStore: pengaturan (hotkey, Preset, AutoFreeze, kecepatan kunci) dibaca dari %AppData%\Ramreader.Trainer\settings\<nama game>.json; tanpa berkas itu Trainer memakai nilai bawaan. MainViewModel: tombol Buka Profil hanya memuat profil, pengaturan tetap dari AppData; ada peringatan versi game lewat MainModuleSize. Kedua .csproj (net10.0-windows, UseWPF) tidak punya ApplicationIcon. Belum diperiksa: PatchItem (apakah hotkey bawaan terisi tanpa settings) dan MainWindow.xaml (tampilan tanpa profil).

**Ikon.** AI membuat dua .ico (chip RAM: R biru muda untuk Ramreader, T oranye untuk Trainer) dan user MENOLAK (jelek, generik). Tidak terpasang di proyek; user tidak menjalankan skrip pemasangan. Lihat aturan 32.

**Tujuan user (diucapkan 3 Nov 2026).**
a. Ikon dengan desain yang TIDAK terlihat buatan AI.
b. Trainer NARUTO standalone: dari data yang sudah didapat (kartu dan patch NSUNSC yang dipakai: Gold Ticket, HP dan God Mode, Chakra dan Unlimited Chakra, Team Tag dengan Team Tag dan Tag Switch, Team Bar dengan Unlimited Team Bar dan Kunci 100 otomatis, KAWARIMI 2 dengan Unlimited Kawarimi), SATU berkas trainer khusus Naruto, tidak butuh Reader. Kata user: itu tujuan membuat Reader sejak awal. Mesin Ramreader dibagikan tanpa profil; trainer Naruto adalah produk terpisah.
c. Ikon untuk trainer Naruto itu juga.
Arah teknis (dugaan, bukan keputusan; desain dulu, aturan 1): profil dan pengaturan perlu tertanam di berkas trainer (mis. EmbeddedResource) supaya tidak bergantung pada %AppData%; Trainer sekarang generik dan memilih profil dari AppData. Pertanyaan terbuka: proyek ketiga di solusi atau mode di Trainer lama; satu .exe atau folder; apakah patch dan pengaturan bawaan ikut tertanam.

**KERJA BERIKUTNYA.**
1. Tanya user SATU pertanyaan: zip v0.1.1 (isi sama dengan v0.1.0) diunggah sebagai Release atau dilewati. Kalau diunggah: GitHub, Releases, Create a new release, tag v0.1.1, judul "Ramreader v0.1.1 (beta)", unggah zip, deskripsi Indonesia dan Inggris, centang pre-release.
2. Ikon: tanya gayanya dulu (aturan 32).
3. Trainer NARUTO standalone: desain dulu; minta berkas lewat Set-Clipboard (PatchItem, FeatureItem, MainWindow.xaml, TrainerSession).
4. Push commit: keputusan user, jangan disuruh.

## 2g. Catatan serah terima 4 Nov 2026 (durasi Awakening, BELUM SELESAI). BACA INI SETELAH 2f

Hanya mencatat pekerjaan satu sesi AI. Penentu durasi Awakening BELUM ditemukan.

**Tujuan user.** Awakening tanpa batas waktu (durasinya selalu kurang). Game: NSUNSC. Istilah resmi (halaman Bandai Namco Eropa en.bandainamcoent.eu/node/8857): Storm gauge = gauge terkecil di bawah gauge chakra, terisi saat memukul atau terkena serangan; kalau penuh bisa dipakai untuk teknik rahasia, combination secret technique, atau Awakening; Awakening menghabiskan seluruh chakra. Halaman itu TIDAK menjelaskan bagaimana durasi ditampilkan. Pertanyaan yang belum dijawab user: bar mana di HUD yang menyusut selama Awakening.

**Fakta (dilaporkan user dari layar atau stopwatch).**
- Nilai awal durasi 900 = 15 detik (stopwatch user), turun 60 per detik. Dugaan: hitungan frame di 60 fps.
- Cari Nilai (tipe Bilangan bulat 8 byte, mode Nilai berkurang) menyisakan 2 alamat: 0x22FCDDE0088 (548 ke 488) dan 0x22FDB587710 (338 ke 278), keduanya turun 60 per Scan Lanjutan (alamat mentah sesi PID 27028; jangan disimpan). User menyimpan kartu "durasi awekening 3 tanpa verifikasi" dan "durasi awekening 4" di Tersimpan (jalur belum diverifikasi).
- Penulis Kode pada kartu itu menangkap satu instruksi: mov [rdi+rbx+1C0h],esi (89 B4 1F C0 01 00 00, 7 byte) di NSUNSC.exe+73B8D0. Sesi 1: Hit 901, Berubah 901, Turun 900. Sesi 2: Hit 157, Berubah 157, Turun 157.
- Patch buatan user di Trainer: (a) "Awakening Tanpa Batas" = NOP di +73B8D0, mode kosong. Dilaporkan: animasi gauge berhenti, tetapi Awakening tetap habis beberapa detik kemudian. Jadi instruksi ini hanya menulis salinan tampilan atau hitungan turunan dan BUKAN penentu berakhirnya Awakening. Jangan dipatch lagi. (b) "Triger Awekening contoh" = byte F3 0F 11 84 1F BC 01 00 00, terbaca disassembler capstone sebagai movss [rdi+rbx+1BCh],xmm0 (float, empat byte sebelum +1C0 pada struktur yang sama). Kata user: hanya menghitung float yang mengaktifkan Awakening. Dinyalakan sendiri: Awakening tetap habis (dilaporkan user). AI sesi itu TIDAK tahu fungsi persisnya.
- Pola sama dengan Cooldown tagging (+60/+64): menahan salinan tampilan tidak menahan penentunya.

**Dugaan (belum terbukti).** Nilai esi di +73B8D0 dihitung dari variabel lain (mis. 900 dikurangi waktu berjalan), jadi penghitung asli ada di tempat lain; atau tulisan itu hanya pembaruan HUD. Game kemungkinan membandingkan nilai dengan nol atau 0x384 (900) untuk mengakhiri Awakening.

**Tidak diketahui.** Dari mana esi berasal; fungsi persis "Triger Awekening"; bar HUD mana yang menunjukkan durasi; apakah pause membekukan timer; kenapa dua alamat int64 selisihnya tetap 210; kenapa user menemukan nilai float menurut katanya padahal dua alamat yang ketemu bertipe int64 (user bilang durasinya float; belum dicocokkan).

**Langkah yang disiapkan sesi ini.**
- Perintah PowerShell (Administrator, NSUNSC berjalan) yang membaca byte kode game: Add-Type P/Invoke OpenProcess dengan akses 0x410 dan ReadProcessMemory, dari basis modul + 0x73B5D0 sepanjang 0x400 byte (0x300 sebelum dan 0x100 sesudah +73B8D0), mencetak hex 32 byte per baris dan menyalin ke clipboard. Byte di +73B8D0 harus 89 B4 1F C0 01 00 00; kalau 90 x7, toggle "Awakening Tanpa Batas" masih menyala (matikan dulu). Hasilnya belum dilaporkan user. Tulis ulang perintahnya kalau perlu.
- Disassemble: pip install capstone --break-system-packages, lalu Cs(CS_ARCH_X86, CS_MODE_64). Terverifikasi memecahkan dua instruksi di atas. Mulai dari awal fungsi (cari padding CC atau ret sebelum +73B8D0). Cari: dari mana esi dimuat; instruksi yang membandingkan nilai itu dengan nol atau 900; lompatan yang mengakhiri Awakening. Patch dibuat di penentu itu, bukan di penulis HUD. Cek apakah instruksi itu juga dipakai musuh (Mode Tree, bagian 2c).
- Permintaan user (sudah disuruh): fitur "Lihat Kode" di Ramreader, yang menampilkan beberapa instruksi sebelum dan sesudah sebuah lokasi tanpa Cheat Engine; Core sudah memakai disassembler Iced di WriteTracer. BELUM dibangun karena file Ramreader.App belum dibaca sesi ini. Minta dulu lewat satu perintah Set-Clipboard (aturan 14): src\Ramreader.App\App.xaml.cs, src\Ramreader.App\MainWindow.xaml, Views\CharacterObjectsPage.xaml dan .xaml.cs, ViewModels\CharacterObjectsViewModel.cs, src\Ramreader.Core\Debugging\WriteTracer.cs. Setelah itu bangun sesuai pola halaman Objek Karakter, lalu beri perintah tutup, build, buka (aturan 22).

**Peringatan.** Toggle "Awakening Tanpa Batas" mungkin masih menyala di Trainer user: suruh matikan sebelum menutup Trainer (lompatan ke cave bisa tertinggal di game). Profil user kini berisi dua patch buatan itu dan dua kartu durasi; profil user tidak boleh masuk rilis.

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
| 9E | Trainer: patch berlaku untuk seluruh tim user, musuh tidak. Alat "Objek Karakter" di Ramreader untuk mencari penanda sisi/tim di dalam objek karakter; lalu CodeCave membaca penanda itu | Alat terbukti jalan di layar user (pemantauan, Bandingkan). Kandidat penanda: offset +0x44. Stabil lintas ronde terbukti (sesi 4). Belum terbukti: stabil setelah respawn atau match baru, semua musuh punya nilai sama (tanpa P2), dan gauge Team Tag punya penanda. CodeCave Mode Team (Mode = "Team", MarkerOffset 0x44) kode terpasang di CodeCave.cs, CodePatch.cs, PatchItem.cs. HP (God Mode): Mode Team terbukti, lolos 8 uji P1 vs COM dan uji PvP (tim kebal, musuh kena; dilaporkan user). Mode Tree (9E-b) terbukti untuk HP dan chakra, termasuk mirror (dilaporkan user). Cooldown summon (+94BB7A) berhasil dan Unlimited Tag Switch (+94C3B6, Mode Tree) berhasil di satu kondisi (dilaporkan user). Tag Switch dites 3 match (dilaporkan user, rincian kondisi tidak diberikan). Bar TEAM: Kunci 100 otomatis berhasil (dilaporkan user). Unlimited Kawarimi (+94C09E): Mode kosong hanya melindungi karakter utama (dilaporkan user), diubah ke Tree 3 Nov 2026, lalu user melapor trainer berjalan tanpa rincian per kondisi (aturan 25 belum terpenuhi). Release v0.1.0 beta terbit di GitHub, workflow Actions hijau (screenshot user). Tersimpan dibersihkan user menjadi 6 kartu (screenshot). Lihat 2e, 2f, dan 11.11 |
| 10 | Rilis mesin (Ramreader dan Trainer, TANPA profil game) di GitHub Releases | v0.1.0 (beta) terbit (screenshot user). Zip v0.1.1 bersih sudah dibangun (413 berkas, 0 profil atau pengaturan, screenshot user), BELUM diunggah. Lihat 2f dan aturan 28 |
| 11 | Ikon aplikasi (Ramreader, Trainer, dan trainer Naruto) | Belum ada. Ikon buatan AI ditolak user (generik). Tanya gaya dulu, aturan 32. Lihat 2f |
| 12 | Trainer NARUTO standalone (satu berkas, data NSUNSC tertanam, tanpa Reader) | Belum didesain. Itu tujuan user sejak awal. Lihat 2f |
| 9F | Durasi Awakening tanpa batas (NSUNSC) | Penentu BELUM ditemukan. +73B8D0 (mov [rdi+rbx+1C0h],esi) hanya menahan animasi gauge, Awakening tetap habis (dilaporkan user). Nilai awal 900 = 15 detik (stopwatch user). Langkah berikut: dump byte kode dan cari asal esi; fitur Lihat Kode belum dibangun. Lihat 2g |

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
- Hasil diagnostik Mode Team (sesi PID 27268, dilaporkan user dengan screenshot): dilewati 46x, dijalankan 0x; objek terakhir dilewati selalu penanda 0xD70455F6 (sama dengan pemain); musuh tetap kebal di Mode Team, tidak kebal di mode pemain-saja. Kesimpulan: cave menganggap objek musuh sama dengan pemain; penyebab belum ditemukan (kemungkinan: musuh membawa penanda sama di +0x44 saat instruksi jalan, atau byte cave salah). Tes pemisah: Ramreader dengan Tandai Otomatis di sesi yang sama dengan patch mati.
- Sesi PID 27268 (dilaporkan user, screenshot Ramreader): HP +94BAE2, hanya 2 objek tertangkap, keduanya ditandai Tim oleh Tandai Otomatis (penanda +0x44 sama 0xD70455F6), padahal user menyatakan objek kedua musuh (0x1F009E31568). Jadi +0x44 SAJA tidak boleh dipercaya sebagai penanda sisi (bentrok di match ini); klaim "tiga kondisi terpenuhi" ditarik. Dugaan belum terbukti: penanda sebenarnya 12 byte (+0x3C +0x40 +0x44). Kode uji: Tandai Otomatis membandingkan 12 byte dan mencetak ketiga nilai tiap objek (terpasang, belum dites).
- PENYEBAB MUSUH KEBAL DI MODE TEAM DITEMUKAN (sesi PID 27268, Info Ramreader dari user): Tandai Otomatis 12 byte menunjukkan Tim = F70E40E7 80DB7929 D70455F6 dan Musuh = AF51AFFF 57FCDEE8 D70455F6 di +0x3C/+0x40/+0x44. +0x44 sama untuk kedua sisi di match ini, sedangkan cave hanya membandingkan +0x44, jadi musuh dianggap satu tim (dilewati 46x, dijalankan 0x). Bandingkan ronde 1: kandidat hanya +0x3C dan +0x40. Offset yang memisahkan berbeda antar sesi, jadi perbandingan harus 3 dword sekaligus. Perbaikan (kode terpasang, belum dites): CodeCave.BuildTeamDiagMulti dan PatchItem (MarkerStart 0x3C, MarkerWords 3, ComputeMarkers, _immOffsets). Catatan: menutup Trainer paksa saat patch menyala meninggalkan lompatan ke cave lama di game; matikan toggle dulu, tutup normal, atau restart game.
- Diagnostik Mode Team (kode terpasang, belum dites): CodeCave.BuildTeamDiag menghitung dilewati/dijalankan di blok data cave (+0 dilewati, +4 dijalankan, +8 objek terakhir dilewati, +16 objek terakhir dijalankan); PatchItem.Diagnostic menampilkannya di status kartu. Tujuan: mencari kenapa musuh ikut kebal di Mode Team. Tes: pukul musuh tanpa terkena, lalu bandingkan hitungan.
- Tanda Tim/Musuh dibuat user dengan heuristik (kolom Nilai yang berubah), jadi ada kemungkinan satu objek salah tanda.

**Yang BELUM terbukti (jangan dicatat sebagai terbukti sebelum user melapor).**
1. Nilai +0x44 stabil setelah respawn atau match baru. (Lintas ronde Bandingkan dan lintas tiga karakter Tim sudah terbukti di sesi 4; respawn dan match baru belum dites.)
2. Semua karakter di sisi musuh punya nilai +0x44 yang sama. Dalam tes terakhir CPU musuh tidak mau tag, sehingga hanya ada satu musuh. Butuh P2 untuk menguji.
3. Gauge Team Tag/summon (basis objek tim, [rax+2Ch]) punya penanda serupa. Belum dicari.
4. Alat Objek Karakter hanya terbukti di instruksi chakra (+94BC94). Objek tertangkap hanya kalau instruksi itu jalan, yaitu setelah karakter memakai chakra (dilaporkan user; jangan menulis "langsung tertangkap"). Di instruksi HP (+94BAE2, movss [rcx+10h]) belum terbukti: tiap karakter harus kena hit dulu. Kolom Instruksi bawaan adalah 94BC94 (chakra); user mencoba alamat HP dan kena hit tanpa hasil, dugaan: kolom Instruksi belum diganti ke 94BAE2. Mode Team di NSUNSC (catatan lama, usang; hasil terbaru di bagian 2b): God Mode lolos 8 uji, tim kebal dan musuh kena. Penanda pemain terbaca 0xD70455F6 (PID 13384).

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