# Asset Value Analyzer — Uygulama Planı

Bu plan Finmaks şartnamesindeki zorunlu kapsamı önce, bonusları sonra tamamlayacak küçük ve çalışır dikey dilimlere ayrılmıştır.

## 0. Hazırlık ve solution sınırları

- [x] MVC + Razor + Tailwind UI foundation
- [x] Domain, Application, Infrastructure, Web, API ve test proje iskeletleri
- [x] Proje referanslarının içeri doğru bağımlılık gösterecek biçimde kurulması
- [x] İlk backend entity ve veritabanı constraint tasarımının gözden geçirilmesi
- [ ] MSSQL geliştirme ortamının doğrulanması

Bitmiş sayılma ölçütü: Bütün solution restore/build olur ve proje sınırları dokümante edilmiştir.

## 1. Kur senkronizasyonu

Akış:

```text
POST /api/exchange-rates/sync
→ request validation
→ Application senkronizasyon use case'i
→ typed Finmaks HttpClient
→ response doğrulama ve CashChangeRate mapping
→ idempotent EF Core upsert
→ MSSQL
→ response DTO / ProblemDetails
```

- [x] `ExchangeRate` entity ve unique constraint
- [x] Strongly typed Finmaks options modeli
- [ ] Options binding, startup validation ve secret yönetimi
- [x] Typed HTTP client ve kontrollü fake response mapping testi
- [ ] Senkronizasyon use case'i
- [ ] EF Core `DbContext` ve idempotent upsert
- [ ] Migration içeriğinin ve hedef MSSQL'in kontrolü
- [ ] Mapping, duplicate önleme ve hata integration testleri

Bitmiş sayılma ölçütü: Aynı Finmaks cevabı iki kez işlendiğinde duplicate oluşmaz; USD/TRY için `CashChangeRate` kullanıldığı testle kanıtlanır.

## 2. Varlık verisi importu

- [ ] XLSX parser
- [ ] Canonical XML parser
- [ ] Ortak normalize aylık import modeli
- [ ] Dosya, şablon, tarih, sayı ve duplicate ay validation'ı
- [ ] Aynı ay için idempotent upsert ve import özeti
- [ ] Parser/use-case testleri

Bitmiş sayılma ölçütü: Geçerli XLSX ve XML aynı normalize sonucu üretir; hatalı dosyalar anlaşılır validation sonucu verir.

## 3. Yİ-ÜFE verisi importu

- [ ] XLSX ve canonical XML parser'ları
- [ ] Ortak validation ve upsert
- [ ] Parser/use-case testleri

Bitmiş sayılma ölçütü: İki format aynı aylık endeks modeline bağlanır ve duplicate/eksik değerler reddedilir.

## 4. Finansal hesaplama

- [ ] Son iş günü kur seçimi ve en fazla 10 günlük geri arama
- [ ] Nominal, dolarizasyon ve enflasyonizasyon hesapları
- [ ] Eksik varlık/endeks/kur aylarının toplu validation'ı
- [ ] Reference golden testler
- [ ] Bağımsız `CashChangeRate` production fixture testleri

Bitmiş sayılma ölçütü: 14 kolonun formül anlamları testlerle doğrulanır; ara hesaplar `decimal`, yuvarlama yalnız presentation sınırındadır.

## 5. Gerçek MVC rapor akışı

- [ ] Dashboard dosya post işlemleri
- [ ] Backend validation durumlarının mevcut üç adımlı UI'a bağlanması
- [ ] Dosyalardan bulunan ay aralığına göre tarih seçimi
- [ ] Gerçek KPI ve 14 kolonlu sonuç view model'i
- [ ] Başarılı ve eksik-veri HTTP integration testleri

Bitmiş sayılma ölçütü: Kullanıcı iki dosyayı yükleyip tarih aralığı seçerek gerçek hesaplanmış raporu görüntüler.

## 6. Bonus ve teslim

- [ ] Ayrı API projesinden güncel kurları DTO ile sunma
- [ ] Hangfire bootstrap/backfill ve 5 dakikalık güncel kur işi
- [ ] SignalR bildirimi ve kontrollü UI refetch
- [ ] Docker/MSSQL çalıştırma yolu
- [ ] GitHub Actions build/test
- [ ] README, sentetik örnekler, secret ve repository son kontrolü

Bitmiş sayılma ölçütü: Temiz makinede dokümante edilen komutlarla restore, CSS build, migration, build ve test çalışır.

## Öncelik ve süre koruması

Zorunlu import, kur ve hesap akışları tamamlanmadan grafik, authentication, export veya ek framework eklenmez. Süre daralırsa SignalR ve görsel ekstralar zorunlu doğrulamanın önüne geçmez.
