# TR/EN dil ve açık/koyu tema teslim raporu

Branch: `feat/localization-and-themes`. Bu çalışma, henüz merge edilmemiş [PR #24](https://github.com/merveustunel/mediclinic/pull/24) üzerine kuruludur. İlk çalışma güncel `origin/main` (`cd94dfe`) üzerinden başlatıldı; ikinci göreve geçerken `main` yeniden getirildi ve değişmedi. Önceki değişiklikler korunarak commit/push edildi. Dil/tema PR'ının hedefi `feat/admin-appointments-and-ux`; önce #24 değerlendirilmelidir. Otomatik merge yapılmaz.

## Kapsam ve mimari

- Varsayılan Türkçe `tr-TR`; seçilebilir İngilizce `en-GB`. ASP.NET Core Request Localization, View/DataAnnotations Localization ve ortak `.resx` kaynakları kullanılır. Alan adları, servis/API sözleşmeleri ve veritabanı isimleri değiştirilmez.
- Sağ üst TR/EN kontrolü yalnızca izin verilen iki kültürü kabul eden, antiforgery korumalı POST kullanır. Bir yıllık kültür çerezi Secure, HttpOnly ve SameSite=Lax'tır. JavaScript yokken yalnızca yerel geri dönüş adresine yönlendirme yapılır; harici adres `/` ile değiştirilir.
- JavaScript açıkken sayfa yenilenmez; aynı kaynak kataloğu üzerinden menü, alan etiketleri, doğrulama, durum ve boş durum metinleri değiştirilir. Form elemanları ve değerleri korunur. Parolalar dil isteğine veya tarayıcı depolamasına aktarılmaz. Klinik adları, doktor/hasta isimleri ve karar gerekçeleri kullanıcı verisi olarak korunur.
- Tarih gösterimi seçilen kültüre uyar; randevuların İstanbul saat dilimi değişmez. Tutarlar aynı TRY tutarının kültüre uygun sayı gösterimidir; döviz dönüşümü yapılmaz. Formların ISO tarih ve invariant sayısal gönderim sözleşmeleri korunur.
- Identity hata kodları yerelleştirilir; sağlayıcının hesap/rol bilgisi içerebilecek açıklamaları kullanıcıya aktarılmaz. Varsayılan model bağlama ve tarayıcı doğrulama mesajları da yerelleştirilir. Ağ hataları teknik hata metni göstermez.
- Tema varsayılan olarak açıktır. `data-theme` ve semantik CSS değişkenleri arka plan, kart, tablo, menü, form, odak ve durum paletlerini merkezi olarak belirler. Güneş/ay kontrolünün erişilebilir adı ve `aria-pressed` durumu güncellenir.
- Tema tercihi `localStorage` içinde saklanır. CSS yüklenmeden çalışan başlangıç betiği kayıtlı temayı uygular. Depolama engellenirse mevcut sayfada kontrol çalışmaya devam eder. Diğer sekmelerdeki tema değişiklikleri de izlenir.
- Yeni migration yoktur. Identity/rol/klinik kapsam politikaları, demo hesaplar ve mevcut randevular değiştirilmemiştir. Gerçek demo veritabanına migration, seed veya reset uygulanmamıştır.

## Test kanıtı

- Önceki randevu/UX teslimi: 225 benzersiz .NET testi (224 tam koşu + ek eşzamanlı karar testi dahil 10 hedefli test), 9 JavaScript, 5 veritabanı güvenliği testi. [Önceki rapor](admin-appointments-and-ux.md).
- Dil/tema sonrası son takım: **240 .NET, 13 JavaScript ve 5 veritabanı güvenliği testi geçti; 0 başarısız, 0 atlanan**, toplam **258 benzersiz test**. Tam .NET kanıtı `delivery-final.trx`; JavaScript `node --test`, veritabanı güvenliği `python tests/database-safety.test.py` ile çalıştırıldı. Tekrarlanan koşular toplamda tekrar sayılmaz.
- Yeni 15 .NET regresyonu: varsayılan dil, kalıcı güvenli çerez, kültür beyaz listesi, harici yönlendirme/antiforgery, iki dilde doğrulama ve Identity, hasta/personel/yönetici/klinik/uygunluk sayfaları, canlı katalog yanıtı, tutar biçimleri, gizli değerlerin hata mesajlarına yansımaması ve kaynak/biçim değişkeni bütünlüğü.
- Yeni 4 JavaScript regresyonu: tema varsayılanı ve yenileme, depolama hataları/sekme eşitleme, dil değişiminde form/parola/kullanıcı metni korunması ve ağ hatasında kurtarma. Mevcut 9 randevu/haftalık saat testi korunur.
- Veritabanı güvenliği testleri yalnızca geçici sentetik veritabanları kullanır. EF `has-pending-model-changes`: model değişikliği yok. Yerel demo veritabanının migration seviyesi bu kontrolden çıkarılamaz.

## Gerçek tarayıcı doğrulaması

HTTPS geliştirme sunucusu bellek veritabanıyla çalıştırıldı. Yerel geliştirme sertifikası yalnızca test tarayıcısında kabul edildi; uygulamanın HTTPS, cookie veya yetkilendirme politikaları değiştirilmedi. Sunucu test sonunda durduruldu.

- Kayıt ekranında doldurulan ad, soyad, e-posta, doğum tarihi ve parola TR→EN değişiminde korundu; URL aynı kaldı.
- EN tercihi yenileme sonrası korundu; koyu tema hem DOM hem kayıtlı tercih hem gerçek arka plan rengiyle doğrulandı (`rgb(21, 18, 30)`).
- 375×812 görünümde yatay taşma görülmedi. Tema ve dil kontrolleri odak + Enter ile çalıştı; dil ve erişilebilir kontrol adları güncellendi.
- Geçersiz e-posta için tarayıcı mesajı Türkçede “Geçerli bir e-posta adresi giriniz.”, İngilizcede “Enter a valid email address.” oldu; girilen değer korundu.

Bu tarayıcı turu anonim kayıt/tercih ekranlarını kapsar. Hasta/personel/yönetici ekranları HTTP entegrasyon testleriyle doğrulandı; gerçek hesaplarla tüm bu ekranların iki dil ve iki temada görsel kabulü, tablo/boş durum/ret gerekçeleri, haftalık program ve mobil ekran okuyucu kontrolü manuel yapılmalıdır. Önceki rapordaki Central/Dermatology/Ayşe Demo randevusu için manuel kabul adımları geçerlidir.

## Değişen dosyalar

Aşağıdaki listeler iki teslimi ayırır. Kaynak dosyaları, görünümler, kaynak katalogları, testler ve bu rapor kapsamı birlikte gösterilir.

### Randevu ve UX (PR #24)

- [docs/admin-appointments-and-ux.md](../docs/admin-appointments-and-ux.md)
- [src/MediClinic.Application/AppointmentBooking.cs](../src/MediClinic.Application/AppointmentBooking.cs)
- [src/MediClinic.Infrastructure/AppointmentBooking.cs](../src/MediClinic.Infrastructure/AppointmentBooking.cs)
- [src/MediClinic.Infrastructure/StaffDashboard.cs](../src/MediClinic.Infrastructure/StaffDashboard.cs)
- [src/MediClinic.Web/Controllers/AccountController.cs](../src/MediClinic.Web/Controllers/AccountController.cs)
- [src/MediClinic.Web/Controllers/AdminAppointmentsController.cs](../src/MediClinic.Web/Controllers/AdminAppointmentsController.cs)
- [src/MediClinic.Web/Controllers/EmployeeManagementController.cs](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs)
- [src/MediClinic.Web/Controllers/TurkishIdentityErrors.cs](../src/MediClinic.Web/Controllers/TurkishIdentityErrors.cs)
- [src/MediClinic.Web/DemoSeed.cs](../src/MediClinic.Web/DemoSeed.cs)
- [src/MediClinic.Web/Models/AuthModels.cs](../src/MediClinic.Web/Models/AuthModels.cs)
- [src/MediClinic.Web/Models/BookingModels.cs](../src/MediClinic.Web/Models/BookingModels.cs)
- [src/MediClinic.Web/Models/WeeklyAvailabilityModel.cs](../src/MediClinic.Web/Models/WeeklyAvailabilityModel.cs)
- [src/MediClinic.Web/Program.cs](../src/MediClinic.Web/Program.cs)
- [src/MediClinic.Web/Views/AdminAppointments/Details.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/AdminAppointments/Index.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Index.cshtml)
- [src/MediClinic.Web/Views/Appointments/Book.cshtml](../src/MediClinic.Web/Views/Appointments/Book.cshtml)
- [src/MediClinic.Web/Views/Appointments/Details.cshtml](../src/MediClinic.Web/Views/Appointments/Details.cshtml)
- [src/MediClinic.Web/Views/Appointments/List.cshtml](../src/MediClinic.Web/Views/Appointments/List.cshtml)
- [src/MediClinic.Web/Views/Appointments/PatientList.cshtml](../src/MediClinic.Web/Views/Appointments/PatientList.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Index.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Index.cshtml)
- [src/MediClinic.Web/Views/Dashboard/ClinicAdmin.cshtml](../src/MediClinic.Web/Views/Dashboard/ClinicAdmin.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Interval.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Interval.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Weekly.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Weekly.cshtml)
- [src/MediClinic.Web/Views/Shared/_Layout.cshtml](../src/MediClinic.Web/Views/Shared/_Layout.cshtml)
- [src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml](../src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Details.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Index.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Index.cshtml)
- [src/MediClinic.Web/wwwroot/css/site.css](../src/MediClinic.Web/wwwroot/css/site.css)
- [tests/MediClinic.Tests/AdminUxTests.cs](../tests/MediClinic.Tests/AdminUxTests.cs)
- [tests/MediClinic.Tests/BookingIntegrationTests.cs](../tests/MediClinic.Tests/BookingIntegrationTests.cs)
- [tests/MediClinic.Tests/BookingMvcTests.cs](../tests/MediClinic.Tests/BookingMvcTests.cs)
- [tests/MediClinic.Tests/DemoVerificationTests.cs](../tests/MediClinic.Tests/DemoVerificationTests.cs)
- [tests/MediClinic.Tests/EmployeeManagementTests.cs](../tests/MediClinic.Tests/EmployeeManagementTests.cs)

### Dil ve tema (bu PR)

- [docs/localization-and-themes.md](../docs/localization-and-themes.md)
- [src/MediClinic.Web/Controllers/AccountController.cs](../src/MediClinic.Web/Controllers/AccountController.cs)
- [src/MediClinic.Web/Controllers/AdminAppointmentsController.cs](../src/MediClinic.Web/Controllers/AdminAppointmentsController.cs)
- [src/MediClinic.Web/Controllers/AppointmentsController.cs](../src/MediClinic.Web/Controllers/AppointmentsController.cs)
- [src/MediClinic.Web/Controllers/ClinicManagementController.cs](../src/MediClinic.Web/Controllers/ClinicManagementController.cs)
- [src/MediClinic.Web/Controllers/EmployeeManagementController.cs](../src/MediClinic.Web/Controllers/EmployeeManagementController.cs)
- [src/MediClinic.Web/Controllers/PreferencesController.cs](../src/MediClinic.Web/Controllers/PreferencesController.cs)
- [src/MediClinic.Web/Controllers/StaffAppointmentsController.cs](../src/MediClinic.Web/Controllers/StaffAppointmentsController.cs)
- [src/MediClinic.Web/Controllers/TurkishIdentityErrors.cs](../src/MediClinic.Web/Controllers/TurkishIdentityErrors.cs)
- [src/MediClinic.Web/Localization/LocalizedIdentityErrors.cs](../src/MediClinic.Web/Localization/LocalizedIdentityErrors.cs)
- [src/MediClinic.Web/Localization/Ui.cs](../src/MediClinic.Web/Localization/Ui.cs)
- [src/MediClinic.Web/Models/ClinicManagementModel.cs](../src/MediClinic.Web/Models/ClinicManagementModel.cs)
- [src/MediClinic.Web/Models/PatientAppointmentsModel.cs](../src/MediClinic.Web/Models/PatientAppointmentsModel.cs)
- [src/MediClinic.Web/Program.cs](../src/MediClinic.Web/Program.cs)
- [src/MediClinic.Web/Resources/SharedResource.en-GB.resx](../src/MediClinic.Web/Resources/SharedResource.en-GB.resx)
- [src/MediClinic.Web/Resources/SharedResource.resx](../src/MediClinic.Web/Resources/SharedResource.resx)
- [src/MediClinic.Web/Resources/SharedResource.tr-TR.resx](../src/MediClinic.Web/Resources/SharedResource.tr-TR.resx)
- [src/MediClinic.Web/SharedResource.cs](../src/MediClinic.Web/SharedResource.cs)
- [src/MediClinic.Web/Views/Account/AccessDenied.cshtml](../src/MediClinic.Web/Views/Account/AccessDenied.cshtml)
- [src/MediClinic.Web/Views/Account/Login.cshtml](../src/MediClinic.Web/Views/Account/Login.cshtml)
- [src/MediClinic.Web/Views/Account/Register.cshtml](../src/MediClinic.Web/Views/Account/Register.cshtml)
- [src/MediClinic.Web/Views/Account/RegistrationComplete.cshtml](../src/MediClinic.Web/Views/Account/RegistrationComplete.cshtml)
- [src/MediClinic.Web/Views/AdminAppointments/Details.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/AdminAppointments/Index.cshtml](../src/MediClinic.Web/Views/AdminAppointments/Index.cshtml)
- [src/MediClinic.Web/Views/Appointments/Book.cshtml](../src/MediClinic.Web/Views/Appointments/Book.cshtml)
- [src/MediClinic.Web/Views/Appointments/Details.cshtml](../src/MediClinic.Web/Views/Appointments/Details.cshtml)
- [src/MediClinic.Web/Views/Appointments/List.cshtml](../src/MediClinic.Web/Views/Appointments/List.cshtml)
- [src/MediClinic.Web/Views/Appointments/PatientList.cshtml](../src/MediClinic.Web/Views/Appointments/PatientList.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Edit.cshtml)
- [src/MediClinic.Web/Views/ClinicManagement/Index.cshtml](../src/MediClinic.Web/Views/ClinicManagement/Index.cshtml)
- [src/MediClinic.Web/Views/Dashboard/ClinicAdmin.cshtml](../src/MediClinic.Web/Views/Dashboard/ClinicAdmin.cshtml)
- [src/MediClinic.Web/Views/Dashboard/Landing.cshtml](../src/MediClinic.Web/Views/Dashboard/Landing.cshtml)
- [src/MediClinic.Web/Views/Dashboard/Patient.cshtml](../src/MediClinic.Web/Views/Dashboard/Patient.cshtml)
- [src/MediClinic.Web/Views/Dashboard/Staff.cshtml](../src/MediClinic.Web/Views/Dashboard/Staff.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Availability.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Edit.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Edit.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Index.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Interval.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Interval.cshtml)
- [src/MediClinic.Web/Views/EmployeeManagement/Weekly.cshtml](../src/MediClinic.Web/Views/EmployeeManagement/Weekly.cshtml)
- [src/MediClinic.Web/Views/Home/Index.cshtml](../src/MediClinic.Web/Views/Home/Index.cshtml)
- [src/MediClinic.Web/Views/Shared/Error.cshtml](../src/MediClinic.Web/Views/Shared/Error.cshtml)
- [src/MediClinic.Web/Views/Shared/_Layout.cshtml](../src/MediClinic.Web/Views/Shared/_Layout.cshtml)
- [src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml](../src/MediClinic.Web/Views/Shared/_StaffAppointments.cshtml)
- [src/MediClinic.Web/Views/Shared/_StatusBadge.cshtml](../src/MediClinic.Web/Views/Shared/_StatusBadge.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Details.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Details.cshtml)
- [src/MediClinic.Web/Views/StaffAppointments/Index.cshtml](../src/MediClinic.Web/Views/StaffAppointments/Index.cshtml)
- [src/MediClinic.Web/Views/_ViewImports.cshtml](../src/MediClinic.Web/Views/_ViewImports.cshtml)
- [src/MediClinic.Web/wwwroot/css/site.css](../src/MediClinic.Web/wwwroot/css/site.css)
- [src/MediClinic.Web/wwwroot/js/booking.js](../src/MediClinic.Web/wwwroot/js/booking.js)
- [src/MediClinic.Web/wwwroot/js/preferences.js](../src/MediClinic.Web/wwwroot/js/preferences.js)
- [src/MediClinic.Web/wwwroot/js/site.js](../src/MediClinic.Web/wwwroot/js/site.js)
- [src/MediClinic.Web/wwwroot/js/theme-init.js](../src/MediClinic.Web/wwwroot/js/theme-init.js)
- [src/MediClinic.Web/wwwroot/js/weekly-hours.js](../src/MediClinic.Web/wwwroot/js/weekly-hours.js)
- [tests/MediClinic.Tests/AuthenticationTests.cs](../tests/MediClinic.Tests/AuthenticationTests.cs)
- [tests/MediClinic.Tests/LocalizationTests.cs](../tests/MediClinic.Tests/LocalizationTests.cs)
- [tests/preferences.test.cjs](../tests/preferences.test.cjs)
- [tests/weekly-hours.test.cjs](../tests/weekly-hours.test.cjs)
