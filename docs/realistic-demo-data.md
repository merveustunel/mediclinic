# Gerçekçi sentetik hastane demo verileri

Bu veri kümesindeki kişiler, iletişim bilgileri, adresler ve randevu senaryoları tamamen sentetiktir. İsim benzerlikleri tesadüfidir. Ücretler yalnızca demo içindir; gerçek hastane tarifesi değildir. Telefonlar `+90 000` önekiyle aranamaz yer tutuculardır. E-postalar `mediclinic.test` alanını kullanır.

## Git ve veri güvenliği

Başlangıç: temiz `main`, HEAD ve fetch sonrası `origin/main`: `7862705e7ce28406e9da4ce0f91d0c72b274865b`. PR #24 (`a898b6f`) ve #25 (`7862705`) main geçmişinde birleşmiştir. İzlenen dosyaların index ve çalışma kopyası LF idi; satır sonundan kaynaklanan içerik farkı yoktu. Çalışma ağacı temizken `feat/realistic-hospital-demo-data` oluşturuldu.

Mevcut yerel veritabanı yalnızca salt okunur bağlantıyla incelendi: 1 organizasyon, 2 şube (`Central`, `Riverside`), 2 bölüm, 6 kişi, 2 hizmet, 5 randevu ve 3 hesap. Uygulama veritabanında seed, reset, migration veya hesap değişikliği yapılmadı. Başlangıç SHA256: `92046304ee79da7baf971f3b8ff73a59519da15c365415f765cc7a9f88088883`.

Yerel DB yalnızca ilk üç migration'ı içeriyor. Mevcut `20261112154350_ClinicManagement`, `20261112161952_StaffManagement` ve `20261112164410_BookingIntegration` eksik. Yeni seed bu DB üzerinde doğrudan çalıştırıldığında işlem yapmadan reddeder. Gerçek DB yükseltmesi bu çalışmanın parçası olarak otomatik yapılmadı; yedek ve migration runbook'u üzerinden ayrıca uygulanmalıdır. Yeni migration oluşturulmadı.

## Veri kümesi ve sayılar

Yeni komut [RealisticDemoSeed](../src/MediClinic.Web/RealisticDemoSeed.cs) içinde tanımlanır. Önceki [DemoSeed](../src/MediClinic.Web/DemoSeed.cs) komutu ve hesapları korunur. İki komut bağımsızdır; yeni küme için eski seed'i çalıştırmak gerekmez.

| Kayıt | Yeni, boş ve mevcut migration'ları uygulanmış DB | Önceki demo üzerine eklenecek |
| --- | ---: | ---: |
| Hastane organizasyonu | 1 | 0 (mevcut demo organizasyonu kullanılır) |
| Şube | 2 | 0 (mevcut şube kimlikleri kullanılır) |
| Bölüm | 8 | 8 |
| Doktor | 16 | 16 |
| Hizmet | 24 | 24 |
| Hasta | 30 | 30 |
| Randevu | 60 | 60 |
| Hasta hesabı | 30 | 30 |
| Hasta kabul personeli ve hesabı | 2 | 2 |
| Klinik yöneticisi hesabı | 1 | 1 |

Eski demo kurulumu korununca toplamlar 10 bölüm, 19 doktor, 26 hizmet ve 31 hasta olur. Ön kontroldeki yerel DB üzerine kullanıcı komutu çalıştırırsa 65 randevu ve 36 hesap beklenir; bu sayılar mevcut DB'de seed çalıştırılmış gibi sunulmamaktadır. Çalışan komut, transaction öncesi/sonrası **gerçek sorgu sonuçlarını** ve tüm randevuların durum sayılarını konsola yazar. Parola veya hash raporlamaz.

Yeni DB'de şube adları **MediClinic Merkez Hastanesi** ve **MediClinic Serdivan Polikliniği** olur. Eski DB'deki `Central` ve `Riverside` adları, adresleri, kimlikleri ve ilişkileri değiştirilmez. Eski şubelere bağlı bölümler/doktorlar taşınmaz. Modelde bölüm doğrudan tek şubeye bağlı olduğundan sekiz bölüm iki şubeye dörder dağıtılır; aynı bölümün iki şubede çoğaltılması yapılmaz.

| Bölüm | Şube | Doktorlar | Hizmetler |
| --- | --- | --- | --- |
| Dahiliye | Merkez | Selin Arısoy, Emre Gökçen | Dahiliye Muayenesi; Metabolik Risk Değerlendirmesi; Diyabet Takibi |
| Dermatoloji | Serdivan | İdil Karaca, Bora Erdem | Dermatoloji Muayenesi; Dermatoskopik İnceleme; Akne Takibi |
| Kardiyoloji | Merkez | Defne Aksu, Kerem Soylu | Kardiyoloji Muayenesi; EKG; Ekokardiyografi |
| Nöroloji | Serdivan | Derya Ilgaz, Ozan Sezer | Nöroloji Muayenesi; Baş Ağrısı Değerlendirmesi; Nörolojik Kontrol |
| Ortopedi ve Travmatoloji | Merkez | Eylül Taner, Mert Akpınar | Ortopedi Muayenesi; Eklem Değerlendirmesi; Spor Yaralanması Kontrolü |
| Göz Hastalıkları | Serdivan | İpek Yalın, Alp Demirtaş | Göz Muayenesi; Göz Tansiyonu Ölçümü; Görme Keskinliği Değerlendirmesi |
| Kulak Burun Boğaz | Merkez | Seda Günay, Umut Eroğlu | KBB Muayenesi; Odyometri; Baş Dönmesi Değerlendirmesi |
| Çocuk Sağlığı ve Hastalıkları | Serdivan | Aslı Sancak, Cem Öztuna | Çocuk Sağlığı Muayenesi; Sağlam Çocuk İzlemi; Büyüme ve Gelişme Takibi |

Her doktor yalnızca kendi bölümünün üç hizmetini sunar. İkinci hizmet 15, diğerleri 30 dakikadır. Demo fiyatları bölüm indeksine (0–7) ve hizmet indeksine (0–2) göre `650 + bölüm × 110 + hizmet × 75` TRY olarak belirlenir; 24 farklı tutar 650–1570 TRY aralığındadır. Randevu fiyatı domain constructor'ıyla oluşturma anında sabitlenir.

Yaşlar ilk kurulum tarihinde yaklaşık 2–89 aralığında; doğum tarihleri, isimler ve hasta hesapları kalıcıdır. Çocuk doktorlarına ilk beş çocuk hasta, yetişkin branşlarına yetişkin hastalar bağlanır.

## Randevu ve çalışma saatleri

Saat dilimi mevcut `Scheduling` kurallarıyla Europe/Istanbul'dur. Eksik şube günleri hafta içi 08:00–18:00 açık, hafta sonu kapalı olarak eklenir; mevcut çalışma saati kayıtları değiştirilmez. Doktorlar 09:00–12:00 ve 13:00–17:00 çalışır. Randevular şube saatlerine, hizmet süresine ve doktor uygunluğuna uyar; hasta ve doktor çakışmaları tüm durumlar için önlenir. Sonraki 14 güne hafta içi seansları eklenir; tekrar çalıştırma yalnızca ileri tarihli uygunluk ufkunu genişletebilir.

İlk kurulumda 24 geçmiş, 12 bugüne yakın ve 24 ileri tarihli randevu hedeflenir. Hafta sonu kurulumunda referans gün önceki Cuma alınır; kapalı günler ve dolu saatler güvenli bir sonraki güne kayar. Mevcut şube saatleri uygun slot bırakmıyorsa işlem hata verip geri alınır.

| Durum | Yeni randevu sayısı | Senaryo |
| --- | ---: | --- |
| Tamamlandı | 12 | Geçmiş kontrol ve muayeneler; Pending → Confirmed → Completed |
| Reddedildi | 6 | Gerekçeli alternatif değerlendirme planı |
| İptal edildi | 6 | Gerekçeli hasta plan değişikliği |
| Beklemede | 18 | Hasta kabul onayı bekleyen istekler |
| Onaylandı | 18 | Planlanmış muayeneler |

Geçmiş randevular Application `Book` servisinin bilinçli olarak yasakladığı geçmiş zaman gerektirdiği ve servis kendi transaction'ını açtığı için tüm demo randevuları tek seed transaction'ı içinde domain `Appointment` constructor ve geçiş metotlarıyla oluşturulur. Hizmet ataması, şube saatleri, uygunluk kapsamı, süre, hasta/doktor çakışması kontrol edilir; `Complete(now)` gelecekte tamamlanmayı engeller. Mevcut SQLite FK/check/overlap trigger'ları da devrededir. Mevcut randevular güncellenmez; kullanıcının sonradan değiştirdiği durumlar korunur. Randevu kimlikleri tarih içermediğinden sonraki günlerde tekrar 60 yeni randevu oluşmaz. İlk randevu kalıcı tarih referansıdır; demo kayıtlarını elle silerek yeni senaryo üretmeyin.

## Güvenli çalıştırma

Normal uygulama açılışında seed çalışmaz. Yalnızca **Development**, **Demo:Enabled=true** ve **--seed-realistic-demo** birlikte kullanılır. Production, Staging ve Testing veritabanı erişiminden önce reddedilir. Yeni demo hesapları da eski demo hesapları gibi Development dışında giriş yapamaz.

Sunucuyu ve diğer DB yazıcılarını durdurun, tek seed süreci çalıştırın. Mevcut DB'nin yolunu doğrulayın; SQLite backup aracı WAL içeriğini de güvenli kopyalar. Seed migration uygulamaz veya DB sıfırlamaz. Eksik migration varsa açıkça hata verir; mevcut [migration runbook](./database-migration-runbook.md) üzerinden ayrı değerlendirin.

PowerShell, depo kökünden:

```powershell
$demoDb = 'C:\Projects\mediclinic\src\MediClinic.Web\mediclinic.db'
if (-not (Test-Path -LiteralPath $demoDb -PathType Leaf)) { throw 'Mevcut demo DB bulunamadı.' }
$demoBackup = $demoDb + '.before-realistic-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak'
python tools/database-safety.py backup $demoDb $demoBackup
if ($LASTEXITCODE -ne 0) { throw 'Yedek alınamadı; seed çalıştırılmadı.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MediClinic = "Data Source=$demoDb;Mode=ReadWrite;Foreign Keys=True"
$env:Demo__Enabled = 'true'
$env:Demo__PatientPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Hasta demo parolası' -AsSecureString)).Password
$env:Demo__StaffPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Personel/yönetici demo parolası' -AsSecureString)).Password
try {
    dotnet run --no-launch-profile --project src/MediClinic.Web -- --seed-realistic-demo
    if ($LASTEXITCODE -ne 0) { throw 'Seed başarısız; hata mesajını inceleyin. Transaction geri alındı.' }
} finally {
    Remove-Item Env:Demo__PatientPassword, Env:Demo__StaffPassword, Env:Demo__Enabled -ErrorAction SilentlyContinue
}
```

Parolalar en az 12 karakter ve Identity'nin büyük/küçük harf, rakam, sembol koşullarını karşılamalıdır. Her çalıştırmada dışarıdan verilir, kaynakta tutulmaz, konsola yazdırılmaz. Var olan parolalar değiştirilmez. Kimlik/e-posta/rol/kişi bağlantısı çakışmaları hesabı devralmak yerine bütün transaction'ı iptal eder.

| Hesap | Rol | Görünüm ve kapsam |
| --- | --- | --- |
| `hospital.patient.01@mediclinic.test` … `hospital.patient.30@mediclinic.test` | Patient | Kendi sentetik randevuları |
| `hospital.staff.1@mediclinic.test` | Staff | Merkez Dahiliye hasta kabulü |
| `hospital.staff.2@mediclinic.test` | Staff | Serdivan Dermatoloji hasta kabulü |
| `hospital.admin@mediclinic.test` | ClinicAdmin | Yalnızca mevcut demo organizasyonuna açık ClinicAdministrator grant'i |

Personel kapsamı mevcut modelde **bölümdür**; bu hesaplar şubenin tüm bölümlerine yetkiliymiş gibi sunulmaz. Yönetici her iki şubenin toplamını mevcut ClinicAdmin ekranından görür. Eski demo hesapları ve yetkileri korunur; model veya yetki kontrolü değiştirilmez.

## Dil, tema ve doğrulama

Veri adları Türkçedir. Mevcut modelde bölüm/hizmet kayıtlarının dil karşılıklarını saklayan alan veya ilişki yoktur. İngilizce arayüzde statik başlıklar mevcut TR/EN kaynaklarından çevrilir; klinik verileri Türkçe kalır. Veri çevirisi ayrı bir gelecek ihtiyaçtır; bunun için migration eklenmedi. Özel isimler çevrilmez. UI/CSS/tema kaynakları değiştirilmedi.

[RealisticDemoSeedTests](../tests/MediClinic.Tests/RealisticDemoSeedTests.cs), geçici SQLite DB ve gerçek migration'larla ilk/tekrar/ileri tarihte seed, hafta sonları, kesin sayılar, fiyat/süre/atamalar, doktor/hasta/uygunluk çakışmaları, durumlar, eski hesap hash'leri ve randevular, bölüm ve klinik izolasyonu, kapalı ortamlar, geçersiz parolalar ve hesap çakışması rollback'ini denetler.

```powershell
dotnet build MediClinic.sln
dotnet test MediClinic.sln
node --test tests/booking-cascade.test.cjs tests/preferences.test.cjs tests/weekly-hours.test.cjs
python tests/database-safety.test.py
```

Uygulama DB'sine otomatik seed yapılmaz. Gerçek eklenen sayıların doğrulama ortamı ve test sonuçları aşağıda kaydedilir.

### Doğrulama sonuçları — 2026-11-13

- Windows `.NET build`: başarılı, 0 uyarı / 0 hata.
- WSL Ubuntu, .NET 8, bağımsız geçici kaynak kopyası: **251/251 .NET testi geçti**, 0 atlanan. 240 mevcut teste 11 yeni seed vakası eklendi. Kaynak kopyasına DB'ler, Git metadata'sı ve Windows build çıktıları dahil edilmedi.
- JavaScript: **13/13**; Python veritabanı güvenlik testleri: **5/5** geçti.
- Windows kısıtlı çalıştırmada Event Log erişim hataları oluştu. Normal izinlerle çalıştırmada beş mevcut yerelleştirme testi İngilizce kaynak metin beklentisinde başarısız oldu; bunlar Windows üzerinde geçti olarak raporlanmıyor. Aynı yerelleştirme testleri WSL'deki tam pakette geçti.
- Mevcut fiyat snapshot testinde tüm HTML içinde `999` arayan kontrol rastgele GUID'lerde yanlış alarm verebiliyordu. Kontrol `data-money` fiyat alanına daraltıldı; snapshot beklentisi korunuyor.
- Geçici, migration uygulanmış boş DB'lerde gerçekten 8 bölüm, 16 doktor, 24 hizmet, 30 hasta, 60 randevu ve 33 hesap oluştu. Aynı gün ve sekiz gün sonra tekrar çalıştırmada bu sayılar ve parola hash'leri değişmedi; yalnızca ileri uygunluk ufku genişledi.
- Eski seed + mevcut randevu senaryosunda gerçekten 19 doktor, 10 bölüm, 26 hizmet, 31 hasta ve 61 randevu doğrulandı; eski hesap hash'leri ve randevu durumu korundu.
- Yerel DB'nin SQLite online backup ile alınan **geçici kopyasına** mevcut üç eksik migration uygulandı. Eski tabloların tüm özgün kolon değerlerinin korunduğu doğrulandı. Ardından yeni seed iki kez çalıştırıldı: her iki çalıştırmada toplamlar **1 organizasyon, 2 şube, 10 bölüm, 19 doktor, 31 hasta, 4 doktor dışı personel, 26 hizmet, 65 randevu, 36 hesap** olarak kaldı. Durumlar: **13 Completed, 19 Confirmed, 19 Pending, 7 Rejected, 7 Cancelled**. İlk seed öncesindeki her satır sonrasında da aynı kaldı; ikinci seed tüm tablo içeriklerini aynen korudu. `integrity_check` ve `foreign_key_check` başarılıydı.
- Asıl Windows DB'sinin SHA256 değeri işlem sonunda başlangıçla aynı kaldı: `92046304ee79da7baf971f3b8ff73a59519da15c365415f765cc7a9f88088883`.
