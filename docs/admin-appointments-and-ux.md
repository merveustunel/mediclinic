# Yönetici randevuları ve çalışan deneyimi

Temel: 13 Kasım 2026 tarihinde getirilen origin/main (`cd94dfe`). Branch: `feat/admin-appointments-and-ux`.

## Davranış

- Klinik yetkileriyle sınırlı tüm randevular, başlangıç tarihine göre artan sırada; başlangıç/bitiş tarihi (dahil) ve durum filtresiyle listelenir. Yönetici paneli de kronolojiktir ve detay bağlantıları sunar.
- Klinik yöneticisi farklı bölümlerdeki bekleyen talepleri onaylayabilir veya gerekçesiyle reddedebilir. Personel ekranının bölüm kapsamı değişmez. Yöneticiye personel rolü veya çalışan bağlantısı eklenmez.
- Kararlar ortak domain geçişleri ve mevcut EF eşzamanlılık kontrolü üzerinden kaydedilir. Başka klinik kimlikleri detayda ve POST işleminde reddedilir.
- Haftalık doktor uygunluğu en fazla 91 günlük seçimden mevcut modelde ayrı günlük aralıklar üretir. Çakışma veya geçmiş aralık varsa hiçbir kayıt yazılmaz. Mevcut uygunluk/randevular değiştirilmez. Sabah ve öğleden sonra programları ayrı işlemlerle tanımlanabilir.
- Çalışan eylemleri ikonlu, açıklamalı, en az 44 piksel yüksekliğinde butonlardır. Tek seferlik uygunluk ekranı korunur; doktor uygunluğu ile şube saatleri ayrımı açıklanır.
- Varsayılan kültür tr-TR; kayıt/giriş, bağlama ve Identity hataları Türkçedir. Identity sağlayıcısının hesap bilgisi içerebilecek hata açıklamaları doğrudan gösterilmez. Yeni demo kayıtları Türkçe etiketlerle oluşturulur; mevcut demo veritabanına seed uygulanmamıştır.

## Otomatik doğrulama

- Tüm .NET: 224 geçti, 0 başarısız, 0 atlandı. Sonradan eklenen farklı bölüm/eşzamanlı karar testi dahil hedefli koşuda 10 test geçti. Son kaynak için benzersiz .NET test sayısı 225 (215 mevcut + 10 yeni); JS ve veritabanı güvenliğiyle toplam 239.
- JavaScript: 9 geçti, 0 başarısız.
- Veritabanı güvenliği: 5 geçti, 0 başarısız; geçici sentetik veritabanları kullanıldı.
- EF model kontrolü: bekleyen model değişikliği yok. Migration eklenmedi; demo veritabanı migrate edilmedi veya sıfırlanmadı.

## Manuel tarayıcı kabulü

1. ClinicAdmin hesabında tüm randevular bağlantısını, kronolojik sıralamayı, tarih sınırlarını ve durum filtrelerini deneyin. Central/Dermatology/Ayşe Demo 16.11.2026 11:00 kaydını detaydan inceleyin; uygun deneme kaydında onay ve gerekçeli ret yapın.
2. İkinci bölümdeki kaydın yönetilebildiğini, başka klinik URL/POST kimliğinin reddedildiğini ve personel ekranının bölümle sınırlı kaldığını kontrol edin.
3. İki sekmede aynı talebi farklı kararla değerlendirin; ikinci işlem güncel durum uyarısı göstermelidir.
4. Haftalık uygunlukta gün seçimi, tarih aralığı, öğle arası için ikinci aralık, çakışmada tüm işlemin iptali ve tek seferlik düzenleme akışlarını deneyin.
5. Mobil görünüm, klavye odakları, ikonların ekran okuyucu açıklamaları ve Türkçe doğrulama mesajlarını inceleyin.

Otomatik MVC testleri gerçek tarayıcı kabulünün yerine geçmez. Bu aşamada gerçek tarayıcı turu yapılmamıştır.
