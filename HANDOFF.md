# HANDOFF

Last done: **Yeni sohbet - bu HANDOFF'un GUNCELLIGI dogrulandi.** Kullanici sohbete eski
(bayat) bir HANDOFF.md/TASKS.md kopyasi yukledi - bu proje-koku dosyasi (01.09.2026,
T74_78_TestClient build referansi) gercek son durumu yansitiyordu, uzerine yazilmadi, ondan
devam edildi (CLAUDE.md ilkesi: "Never trust documentation over live state" - canli proje
execute_code ile tarandi, T53/T54/T55/T74-T78'in kodda TAM oldugu ama TASKS.md'de hala `[ ]`
kaldigi dogrulandi - bu DOGRU, cunku hicbiri canli 2-client testiyle onaylanmadi).

Bu oturumda: Play mode baslatildi, MainMenuController.createLobbyButton.onClick.Invoke() ile
host lobisi yeniden acildi (client0, konsol temiz - sadece pre-existing/ilgisiz Input System
uyarilari ve kinematic-body uyarilari var, bunlara dokunulmadi). **Client henuz acilmadi/
baglanmadi - kullanicidan Builds\T74_78_TestClient\FarmerSquad.exe'yi acmasi bekleniyor.**

## Bu oturumda yapılanlar (sırayla)

1. Önceki oturumdan devralınan HANDOFF okundu, açık iş netti: client build eski olduğu için
   T76 3. bugfix (uçan yazı yönü) doğrulanamamıştı.
2. Projedeki build klasörleri tarandı (`Builds/` altında 6 aday) — en son değiştirilen ve bu
   test aşamasıyla isim uyumlu olan `T74_78_TestClient` klasörü hedef seçildi, kullanıcıya
   doğrulatıldı (itiraz gelmedi).
3. Play mode durduruldu → `BuildPipeline.BuildPlayer` ile aynı path'in üzerine yeniden build
   alındı (StandaloneWindows64, tek sahne `Assets/Scenes/SampleScene.unity`). Build derleme
   hatasızdı (sadece önceden var olan, ilgisiz `ServerRpc.RequireOwnership` obsolete
   uyarıları + 1 `OnDestroy` hide uyarısı görüldü — dokunulmadı, kapsam dışı).
   **NOT: Build ~birkaç dakika sürdü ve bu sırada Unity ana thread'i tamamen kilitlendi,
   MCP bridge de bu sürede timeout verdi — bu BEKLENEN bir durumdur, hata değildir.**
4. Play mode tekrar başlatıldı, host `Lobi Oluştur` ile başlatıldı (port sorunu YAŞANMADI bu
   sefer — muhtemelen bir önceki oturumda Editor'ün tam kapatılıp açılmış olması sayesinde).
5. Kullanıcı testi sonraya bırakmak istedi — **client (yeni build) hiç açılmadı, bağlanmadı,
   T76 hâlâ görsel olarak doğrulanmadı.** Host play mode'da lobi ekranında (tek oyuncu, host)
   bekliyor durumda bırakıldı.

## Hâlâ açık olanlar

- **T76 GÖRSEL DOĞRULAMASI HÂLÂ YAPILMADI** — ama artık engel yok: build güncel, host hazır.
  Sıradaki oturumun/devamın İLK işi bu olmalı.
- **T77 (panel zıplaması) ve late-join koruması** — hâlâ hiç test edilmedi.
- **FAZ 12 açık checklist:** 2-client network testi (lobi/karakter/ready akışı önceki oturumda
  doğrulandı, ama tam oyun içi 2-client testi hâlâ yok) ve Host-only Start Game kısıtlaması
  hâlâ eklenmedi.
- **AG_MIMARISI_GUNCELLEME_PATCH.md** — hâlâ kullanıcı tarafından uygulanması bekleniyor.
- **T71-73** — hâlâ onay bekliyor, başlatma.
- **CREDITS.md'deki eksik kayıtlar** — hâlâ sorulmayı bekliyor.

## Sıradaki oturumda yapılması gereken (öncelik sırası)

1. **Play mode zaten host olarak açık olabilir** (kontrol et — `NetworkManager.Singleton.
   IsListening`) — açıksa yeniden başlatmaya gerek yok, direkt kullanıcıdan
   `Builds\T74_78_TestClient\FarmerSquad.exe`'yi açıp bağlanmasını iste. Kapalıysa host'u
   yeniden başlat (`Lobi Oluştur` onClick.Invoke ile, reflection değil).
2. Client bağlanınca: karakter seç, Hazır'a bas, host'u da hazır+başlat yap, satış yaptır →
   uçan yazının **panelin ALTINA doğru** kaydığını (yukarı/HUD üstüne değil, ekran dışına
   taşmadan) doğrula.
3. T77 (panel zıplaması) ve late-join korumasını test et.
4. Hepsi geçerse: TASKS.md'de T74-T78 ve FAZ 12'nin 2-client maddesini `[x]` yap.
5. AG_MIMARISI_GUNCELLEME_PATCH.md uygulandı mı kontrol et.
6. T71-73 hâlâ onay bekliyor — başlatma.

Current task: **T76'nın (uçan yazı, panelin altına doğru kayma) client ekranında görsel
doğrulanması — build ve host hazır, sadece testin kendisi yapılmadı.**

Constitution reminders that matter now:

- **Build süresi boyunca MCP bridge timeout verir (birkaç dakika) — bu normaldir, hata
  sanılıp tekrar tekrar build tetiklenmemeli.** Build bitip bitmediğini exe dosyasının
  `LastWriteTime`'ı ile kontrol et.
- Script değişikliği → Play mode'da aktif NGO oturumu varsa MUTLAKA önce Stop, sonra
  değiştir, sonra tekrar Play (bu oturumda tekrar uygulandı, port sorunu YAŞANMADI).
- **C# script değişikliği SADECE Editor'ü (host) etkiler, client BUILD'i etkilemez** — build
  değiştiyse yeniden alınmadan client testi ANLAMSIZDIR (bu oturumda bu yüzden build alındı).
- `EditorApplication.isPaused` ile 2-client oturumda host'u dondurma — client'ı disconnect
  edebilir (hâlâ geçerli, kullanılmadı).
- Host/client start-stop: UI butonu `onClick.Invoke()`, reflection ile değil.
- Ağ mimarisi = LAN UnityTransport (kalıcı geliştirme kararı), Steamworks yayın öncesi ayrı iş.
- T71-73 hâlâ onay bekliyor — başlatma.

Deviations / notes:

- Bu oturumda kod DEĞİŞMEDİ — sadece önceki oturumun kod değişikliğiyle build alındı.
- Değişen/oluşan dosyalar: `Builds\T74_78_TestClient\FarmerSquad.exe` (+ ilişkili _Data
  klasörü) yeniden build edildi, üzerine yazıldı.
- Oturum, kullanıcının 'testi sonraya bırakalım' talebiyle burada durduruldu — host play
  mode'da açık ve lobi oluşturulmuş halde bırakıldı (bir sonraki oturum devam edebilir).

---

**Sonraki sohbette:** Yeni sohbet başlat, bu `HANDOFF.md` + güncel `TASKS.md` ekle, `UYGULA` yaz.
İlk iş: host'un hâlâ play mode'da/lobide olup olmadığını kontrol et, kullanıcıdan güncel build'i
açıp bağlanmasını iste, T76'yı görsel doğrula.
