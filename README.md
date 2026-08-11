# AssetValueAnalyzer

AssetValueAnalyzer; aylık varlık ve Yİ-ÜFE verilerini, Finmaks üzerinden alınan
USD/TRY kurlarıyla birleştirerek nominal değişim, dolarizasyon etkisi ve enflasyon
etkisi hesaplayan bir ASP.NET Core uygulamasıdır.

Kullanıcı iki XLSX dosyasını yükler, rapor dönemini seçer ve 14 kolonlu finansal
etki raporunu web arayüzünde görüntüler veya XLSX olarak indirir.

## Özellikler

- Sabit şirket şablonlarına uygun Varlık ve Yİ-ÜFE XLSX importu.
- Dosya boyutu, uzantı, içerik, tarih, sayı ve tekrar eden ay doğrulamaları.
- Finmaks `ExchangeRates` listesindeki tüm para çiftlerinin ve kur alanlarının
  MSSQL'de idempotent olarak tutulması.
- Uygulama başlangıcında Aralık 2021'den bugüne eksik tüm kur verilerinin tamamlanması.
- Hangfire ile varsayılan üç dakikada bir güncel kur kontrolü.
- SignalR ile kur kartının sayfa yenilenmeden güncellenmesi.
- Nominal değişim, dolarizasyon ve enflasyon etkisi hesaplamaları.
- 14 kolonlu detaylı rapor, KPI özeti ve XLSX rapor çıktısı.
- Ayrı, salt okunur kur Web API'si.
- Docker Compose ve manuel kurulum desteği.

## Teknolojiler

- .NET 10 / ASP.NET Core 10 MVC ve Controller-based Web API
- Razor Views, Vanilla JavaScript ve Tailwind CSS 4
- EF Core 10 Code First ve MSSQL
- Hangfire ve SignalR
- Typed `HttpClient` ile Finmaks entegrasyonu
- ClosedXML ile XLSX okuma ve üretme
- xUnit ve `WebApplicationFactory<Program>` integration testleri

## Kaynak kodu bilgisayara alın

Kurulum yönteminden bağımsız olarak önce projeyi bilgisayarınıza indirin. Bunun
için aşağıdaki iki yöntemden yalnız birini kullanın.

### Yöntem A — Git ile klonlama

Bu yöntemde Git repository'si indirilir ve daha sonra `git pull` ile
güncellenebilir.

#### 1. Git kurulumunu kontrol edin

```bash
git --version
```

Komut bir sürüm numarası döndürmelidir. `git: command not found` benzeri bir hata
alırsanız [Git'in resmî indirme sayfasından](https://git-scm.com/downloads)
işletim sisteminize uygun kurulumu tamamlayıp yeni bir terminal açın.

#### 2. Proje için bir ana klasör oluşturun

Terminali projeyi saklamak istediğiniz konumda açın. Aşağıdaki komutlar önce ana
çalışma klasörünü oluşturur, ardından terminali bu klasörün içine taşır:

```bash
mkdir asset-value-analyzer-kurulum
cd asset-value-analyzer-kurulum
```

#### 3. Repository'yi ana klasörün içine klonlayın

```bash
git clone https://github.com/berkayozcann/AssetValueAnalyzer.git
```

Bu işlem bulunduğunuz ana klasörün içinde `AssetValueAnalyzer` adlı yeni bir
repository klasörü oluşturur. Klasör yapısı şu hâle gelir:

```text
asset-value-analyzer-kurulum/
└── AssetValueAnalyzer/
    ├── AssetValueAnalyzer.sln
    ├── compose.yaml
    ├── Dockerfile
    ├── src/
    └── tests/
```

#### 4. Klonlanan repository klasörüne girin

```bash
cd AssetValueAnalyzer
```

### Yöntem B — GitHub'dan ZIP indirme

Bu yöntemde bilgisayarda Git kurulu olmak zorunda değildir.

1. Tarayıcıdan
   [AssetValueAnalyzer GitHub sayfasını](https://github.com/berkayozcann/AssetValueAnalyzer)
   açın.
2. **Code** düğmesine, ardından **Download ZIP** seçeneğine basın.
3. Bilgisayarınızda `asset-value-analyzer-kurulum` adlı bir ana klasör oluşturun.
4. İndirilen ZIP dosyasını bu ana klasörün içine taşıyıp çıkartın.
5. ZIP'ten çıkan proje klasörünü terminalde açın. GitHub bu klasörü genellikle
   `AssetValueAnalyzer-main` adıyla oluşturur:

```bash
cd asset-value-analyzer-kurulum
cd AssetValueAnalyzer-main
```

Çıkan klasörün adı farklıysa ikinci komutta gerçek klasör adını kullanın.

### Proje klasörünü doğrulayın

Git veya ZIP yöntemlerinden hangisini kullandıysanız terminalin son olarak
`AssetValueAnalyzer.sln` dosyasının bulunduğu proje klasöründe olması gerekir.
Doğru klasörde olduğunuzu kontrol edin:

```bash
pwd
ls
```

`ls` çıktısında en az `AssetValueAnalyzer.sln`, `compose.yaml`, `Dockerfile`,
`src` ve `tests` görünmelidir. Bundan sonraki bütün README komutları bu
`AssetValueAnalyzer` repository klasörünün içinde çalıştırılmalıdır.

## Kurulum yolunu seçin

Aşağıdaki üç yol birbirinden bağımsızdır. Bir kurulum sırasında yalnız birini
uygulayın.

| Yol | Uygulama nerede çalışır? | MSSQL nerede çalışır? | Gerekenler |
| --- | --- | --- | --- |
| 1. Tam Docker Compose | Docker container'larında | Docker container'ında | Docker ve Finmaks API anahtarı |
| 2. Manuel + container DB | Bilgisayarda `dotnet run` ile | Bağımsız Docker container'ında | .NET, Node.js, pnpm, Docker ve Finmaks API anahtarı |
| 3. Manuel + mevcut MSSQL | Bilgisayarda `dotnet run` ile | Mevcut/uzak SQL Server'da | .NET, Node.js, pnpm, MSSQL erişim bilgileri ve Finmaks API anahtarı |

## Yol 1 — Tam Docker Compose kurulumu

Bu yol Web, API ve MSSQL'i birlikte Docker'da çalıştırır. Bilgisayara .NET,
Node.js veya pnpm kurmanız gerekmez. Bu yöntemde `user-secrets` kullanılmaz.

### Gereksinimler

- Çalışır durumda Docker Desktop veya Docker Engine
- Docker Compose (`docker compose version` ile kontrol edilebilir)
- Finmaks tarafından verilmiş gerçek API anahtarı

Docker kurulu değilse macOS/Windows için [Docker Desktop](https://docs.docker.com/desktop/),
Linux için [Docker Engine](https://docs.docker.com/engine/install/) kurulumunu
tamamlayın. Docker Desktop kullanıyorsanız uygulamayı açıp engine'in başlamasını
bekleyin. Ardından kontrol edin:

```bash
docker --version
docker compose version
docker info
```

Üç komut da hata vermeden çalışmalıdır. `docker info` daemon bağlantı hatası
veriyorsa Docker Desktop/Engine henüz çalışmıyordur.

### 1. `.env` dosyasını oluşturun

```bash
cp .env.example .env
```

Repository kökündeki yeni `.env` dosyasını bir metin editörüyle açın ve iki
placeholder değeri değiştirin:

```text
MSSQL_SA_PASSWORD=<KENDİNİZİN_BELİRLEDİĞİ_GÜÇLÜ_PAROLA>
FINMAKS_API_KEY=<FINMAKS_TARAFINDAN_VERİLEN_GERÇEK_ANAHTAR>
```

`MSSQL_SA_PASSWORD` hazır gelen bir parola değildir. Kurulumu yapan kişi bu
aşamada yeni ve güçlü bir parola belirler ve `=` işaretinden sonra yazar. Compose
aynı değeri hem MSSQL container'ını oluştururken hem de Web/API connection
string'lerini üretirken kullanır.

MSSQL container parolası şu şartları sağlamalıdır:

- En az `8`, en fazla `128` karakter olmalıdır.
- Büyük harf, küçük harf, rakam ve sembol gruplarının en az üçünden karakter
  içermelidir.

Örneğin `GucluDb!2026` bu yapıyı gösterir; güvenlik için bu örneği aynen
kullanmayın, kendinize ait farklı bir parola belirleyin. Şartlar sağlanmazsa MSSQL
container kurulumu tamamlayamaz ve kapanır. Ayrıntılar için
[Microsoft'un SQL Server container parola kurallarına](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver17#change-the-sa-password)
bakabilirsiniz.

`FINMAKS_API_KEY` kullanıcı tarafından uydurulamaz; Finmaks tarafından sağlanan
gerçek anahtar yazılmalıdır. `.env` git tarafından izlenmez ve commit edilmemelidir.

### 2. Tüm servisleri başlatın

```bash
docker compose up --build --wait
```

İlk build ve Aralık 2021'den bugüne kur senkronizasyonu birkaç dakika sürebilir.
Servis durumlarını kontrol edin:

```bash
docker compose ps
```

`web`, `api` ve `mssql` servislerinin `healthy` olması beklenir.

### 3. Uygulamayı kontrol edin

| Servis | Adres |
| --- | --- |
| Web | `http://localhost:5271` |
| API | `http://localhost:5272` |
| Web health | `http://localhost:5271/health` |
| API health | `http://localhost:5272/health` |
| MSSQL | `localhost,1433` |

Terminalden sağlık kontrolü:

```bash
curl http://localhost:5271/health
curl http://localhost:5272/health
```

Her iki isteğin de `Healthy` ve HTTP `200` döndürmesi beklenir.

### 4. Servisleri durdurun

```bash
docker compose down
```

Bu komut container'ları kaldırır ancak MSSQL named volume'ünü ve verileri korur.
`docker compose down --volumes` veritabanı volume'ünü ve içindeki verileri kalıcı
olarak siler; temiz kurulum istenmiyorsa kullanılmamalıdır.

## Manuel yollar için tam sürüm kurulumu

Bu bölüm yalnız Yol 2 veya Yol 3 uygulanacaksa gereklidir. Projenin son doğrulandığı
tam araç sürümleri şunlardır:

| Araç | Sürüm |
| --- | --- |
| .NET SDK | `10.0.302` |
| Node.js | `24.18.1` |
| pnpm | `11.16.0` |

Proje EF Core Code First migration'larını kullanır. Ancak Web hostu açılışta
bekleyen migration'ları otomatik uyguladığı için manuel kurulum yapan kişinin
`dotnet-ef` aracını kurması veya elle migration komutu çalıştırması gerekmez.

### 1. .NET SDK 10.0.302'yi kurun

[Microsoft .NET 10 indirme sayfasını](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
açın. `SDK 10.0.302` başlığı altında işletim sisteminize ve işlemci mimarinize
uygun **SDK** installer'ını seçin. Yalnız Runtime indirmek yeterli değildir.

- Apple Silicon Mac: macOS Arm64 SDK installer
- Intel Mac: macOS x64 SDK installer
- 64 bit Windows: Windows x64 SDK installer
- Linux: aynı sayfadaki dağıtımınıza uygun package manager talimatı veya binary

Installer bittikten sonra yeni bir terminal açın ve doğrulayın:

```bash
dotnet --version
```

Beklenen çıktı: `10.0.302`.

### 2. Node.js 24.18.1'i kurun

[Resmî Node.js 24.18.1 arşivini](https://nodejs.org/dist/v24.18.1/) açın.

- macOS: `node-v24.18.1.pkg`
- 64 bit Windows: `node-v24.18.1-x64.msi`
- Apple Silicon/Linux gibi diğer mimariler: işletim sistemi ve mimari adını
  taşıyan `24.18.1` paketini

kurun. Kurulumdan sonra yeni bir terminal açıp doğrulayın:

```bash
node --version
npm --version
```

Node çıktısının `v24.18.1` olması beklenir. `npm`, Node installer'ıyla birlikte gelir.

### 3. pnpm 11.16.0'ı kurun

Node kurulduktan sonra:

```bash
npm install --global pnpm@11.16.0
pnpm --version
```

Beklenen pnpm çıktısı: `11.16.0`.

## Yol 2 — Manuel Web/API + container MSSQL

Bu yolda yalnız veritabanı Docker container'ında çalışır. Web ve API doğrudan
bilgisayarda `dotnet run` ile başlatılır. Docker Compose ve `.env` kullanılmaz.

### Gereksinimler

- Bir önceki bölümdeki .NET SDK, Node.js ve pnpm sürümleri
- Çalışır durumda Docker
- Finmaks tarafından verilmiş gerçek API anahtarı
- `1433`, `5271` ve `5076` portlarının başka bir uygulama tarafından kullanılmaması

Docker kurulu değilse macOS/Windows için [Docker Desktop](https://docs.docker.com/desktop/),
Linux için [Docker Engine](https://docs.docker.com/engine/install/) kurulumunu
tamamlayın. Docker Desktop'ı açtıktan sonra `docker info` komutunun hata vermeden
çalıştığını doğrulayın. Bu yolda `docker compose` komutu kullanılmayacaktır.

### 1. Container DB parolasını belirleyin

`<CONTAINER_DB_PAROLASI>` yerine kendinizin belirlediği güçlü bir MSSQL `sa`
parolası yazacaksınız. Bu parolayı iki yerde aynı kullanmanız gerekir:

1. MSSQL container'ını oluştururken `MSSQL_SA_PASSWORD` değerinde.
2. Manuel Web/API için kaydedilen connection string'in `Password` bölümünde.

Parola `8–128` karakter olmalı ve büyük harf, küçük harf, rakam ve sembol
gruplarının en az üçünden karakter içermelidir. Uygulama bu parolayı üretmez veya
sizin yerinize belirlemez. Bu şartlar sağlanmazsa MSSQL container başlatılamaz.

### 2. MSSQL volume'ünü ve container'ını oluşturun

```bash
docker volume create asset-value-analyzer-mssql-data

docker run --detach \
  --name assetvalueanalyzer-mssql \
  --platform linux/amd64 \
  --env ACCEPT_EULA=Y \
  --env MSSQL_PID=Developer \
  --env MSSQL_SA_PASSWORD='<CONTAINER_DB_PAROLASI>' \
  --publish 127.0.0.1:1433:1433 \
  --volume asset-value-analyzer-mssql-data:/var/opt/mssql \
  mcr.microsoft.com/mssql/server:2022-latest
```

Tek tırnakları bırakın; yalnız `<CONTAINER_DB_PAROLASI>` metnini kendi parolanızla
değiştirin. Bu komut parolayı doğrudan MSSQL container'ına verir. `.env` veya
`.NET user-secrets` MSSQL container'ı tarafından okunmaz.

Container'ın hazır olduğunu kontrol edin:

```bash
docker exec assetvalueanalyzer-mssql \
  /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<CONTAINER_DB_PAROLASI>' -C -Q 'SELECT 1'
```

Komut bir satırında `1` döndürmelidir. Container henüz başlıyorsa birkaç saniye
bekleyip aynı kontrolü yeniden çalıştırın. Logları görmek için:

```bash
docker logs assetvalueanalyzer-mssql
```

### 3. Container DB connection string'ini kaydedin

Aşağıdaki connection string formatını proje verir. Siz yalnız
`<CONTAINER_DB_PAROLASI>` bölümünü 1. adımda seçtiğiniz aynı parolayla değiştirirsiniz:

```bash
dotnet user-secrets set "ConnectionStrings:AssetValueAnalyzer" \
  "Server=localhost,1433;Database=AssetValueAnalyzer;User Id=sa;Password=<CONTAINER_DB_PAROLASI>;Encrypt=True;TrustServerCertificate=True" \
  --project src/AssetValueAnalyzer.Web
```

Bu komut tamamlanmış connection string'i bilgisayarınızdaki .NET development
secret store'una kaydeder. Repository'deki herhangi bir dosyaya yazmaz.

Connection string alanlarının anlamı:

- `Server=localhost,1433`: az önce başlatılan MSSQL container'ı
- `Database=AssetValueAnalyzer`: migration ile oluşturulacak proje veritabanı
- `User Id=sa`: container'ın SQL Server yöneticisi
- `Password=...`: `docker run` komutunda verdiğiniz aynı parola

### 4. Finmaks API anahtarını kaydedin

```bash
dotnet user-secrets set "Finmaks:ApiKey" "<FINMAKS_API_KEY>" \
  --project src/AssetValueAnalyzer.Web
```

`<FINMAKS_API_KEY>` yerine Finmaks'ın verdiği gerçek anahtarı yazın. Web ve API
projeleri aynı `UserSecretsId` değerini kullandığı için secret'ları Web projesi
üzerinden bir kez kaydetmek yeterlidir. API yalnız connection string'i kullanır;
Finmaks senkronizasyonunu Web hostu yapar.

Sonraki `dotnet run --launch-profile http` komutları uygulamaları `Development`
ortamında başlatır. .NET bu nedenle yerel `user-secrets` değerlerini otomatik
olarak configuration'a ekler. Connection string'i kodda veya README'de sabit
değer olarak tutmak gerekmez.

### 5. Frontend paketlerini ve assetlerini hazırlayın

```bash
cd src/AssetValueAnalyzer.Web
pnpm install --frozen-lockfile
pnpm run assets:build
cd ../..
```

İlk komut lock dosyasındaki tam frontend bağımlılıklarını kurar. İkinci komut
Tailwind CSS, font ve SignalR browser dosyalarını `wwwroot` altına üretir.

### 6. .NET solution'ını hazırlayın

```bash
dotnet restore AssetValueAnalyzer.sln
dotnet build AssetValueAnalyzer.sln --no-restore
```

Build sonunda hata veya uyarı beklenmez.

### 7. Web'i çalıştırın

Bir terminalde:

```bash
dotnet run --project src/AssetValueAnalyzer.Web --launch-profile http
```

Web adresi: `http://localhost:5271`.

Elle migration komutu çalıştırmanız gerekmez. Web başlangıçta 3. adımda kaydedilen
connection string'i okur ve bekleyen EF Core migration'larını container MSSQL'e
otomatik uygular. İlk kurulumda `AssetValueAnalyzer` veritabanını ve gerekli
tabloları oluşturur. Migration tamamlandıktan sonra eksik Finmaks kur verilerini
MSSQL'e aktarmaya başlar; bu işlem birkaç dakika sürebilir.

Container parolası veya connection string yanlışsa ya da MSSQL henüz hazır
değilse Web başlamaz ve hata bu terminalde görünür. Ayarı düzeltip aynı
`dotnet run` komutunu yeniden çalıştırın.

Web health kontrolünü ikinci bir terminalde yapın:

```bash
curl http://localhost:5271/health
```

### 8. API'yi çalıştırın

Web çalışmaya devam ederken ayrı bir terminalde:

```bash
dotnet run --project src/AssetValueAnalyzer.Api --launch-profile http
```

Manuel API adresi: `http://localhost:5076`.

```bash
curl http://localhost:5076/health
curl "http://localhost:5076/api/exchange-rates/latest?baseCurrencyCode=1&foreignCurrencyCode=56&limit=1"
```

### 9. Manuel kurulumu durdurun ve yeniden başlatın

Web ve API terminallerinde `Ctrl+C` kullanın. MSSQL container'ını durdurmak için:

```bash
docker stop assetvalueanalyzer-mssql
```

Named volume silinmediği için veriler korunur. Daha sonra yeniden başlatmak için:

```bash
docker start assetvalueanalyzer-mssql
```

Ardından Web ve API için 7. ve 8. adımlardaki `dotnet run` komutlarını yeniden
çalıştırın.

## Yol 3 — Manuel Web/API + mevcut veya uzak MSSQL

Bu yolda Docker ve `.env` kullanılmaz. Web ve API bilgisayarda `dotnet run` ile,
veritabanı ise erişebildiğiniz mevcut bir SQL Server üzerinde çalışır. SQL Server
macOS'ta native çalışmadığı için Mac kullanıcısı bu yol için başka bir makinedeki
veya buluttaki SQL Server'a erişmelidir.

### Gereksinimler

- Manuel sürüm kurulum bölümündeki .NET SDK, Node.js ve pnpm
- Finmaks tarafından verilmiş gerçek API anahtarı
- SQL Server sunucu adı veya IP adresi
- SQL Server TCP portu; değiştirilmediyse genellikle `1433`
- SQL kullanıcı adı ve parolası
- Boş `AssetValueAnalyzer` veritabanı veya bu veritabanını oluşturma yetkisi
- Migration'ın tablo ve index oluşturabilmesi için gerekli veritabanı yetkileri

### 1. MSSQL bilgilerini alın

Bu yöntemde MSSQL parolasını AssetValueAnalyzer oluşturmaz. Parola:

- şirket/uzak sunucu yöneticisinin size verdiği mevcut SQL hesabının parolasıdır veya
- SQL hesabını siz yönetiyorsanız hesabı oluştururken sizin belirlediğiniz paroladır.

Sunucu size ait değilse veritabanı yöneticisinden şu beş bilgiyi alın:

1. Sunucu adı veya IP adresi
2. SQL Server portu
3. Veritabanı adı (`AssetValueAnalyzer` önerilir)
4. SQL kullanıcı adı
5. SQL kullanıcı parolası

Hesap veritabanı oluşturamıyorsa yöneticiden boş `AssetValueAnalyzer`
veritabanını oluşturmasını ve hesaba bu veritabanında migration uygulayacak yetki
vermesini isteyin.

### 2. Mevcut MSSQL connection string'ini kaydedin

Proje connection string formatını aşağıda verir. Köşeli placeholder'ların tamamını
1. adımda aldığınız gerçek MSSQL bilgileriyle değiştirin:

```bash
dotnet user-secrets set "ConnectionStrings:AssetValueAnalyzer" \
  "Server=<MSSQL_SUNUCU_ADI>,<MSSQL_PORTU>;Database=<VERİTABANI_ADI>;User Id=<MSSQL_KULLANICISI>;Password=<MSSQL_PAROLASI>;Encrypt=True;TrustServerCertificate=True" \
  --project src/AssetValueAnalyzer.Web
```

Örneğin sunucu `sql.example.local`, port `1433`, veritabanı
`AssetValueAnalyzer` ve kullanıcı `assetvalueapp` ise yalnız bu değerler ile o
kullanıcının gerçek parolası yazılır. README'de hazır veya ortak bir connection
string parolası bulunmaz.

Sunucu yöneticiniz geçerli ve güvenilen bir TLS sertifikası kullanıyorsa
`TrustServerCertificate=True` yerine `False` kullanın. Bağlantı politikasını
sunucu yöneticiniz belirlemelidir.

### 3. Finmaks API anahtarını kaydedin

```bash
dotnet user-secrets set "Finmaks:ApiKey" "<FINMAKS_API_KEY>" \
  --project src/AssetValueAnalyzer.Web
```

`<FINMAKS_API_KEY>` yerine Finmaks'ın verdiği gerçek anahtarı yazın. Connection
string ve API anahtarı yalnız yerel .NET development secret store'unda tutulur;
repository'ye veya `.env` dosyasına yazılmaz.

Web ve API aynı `UserSecretsId` değerini kullandığı için iki secret'ı Web projesi
üzerinden bir kez kaydetmek iki manuel host için de yeterlidir. Sonraki
`dotnet run --launch-profile http` komutları `Development` ortamını seçer ve .NET
bu secret store'u otomatik olarak configuration'a ekler.

### 4. Frontend paketlerini ve assetlerini hazırlayın

```bash
cd src/AssetValueAnalyzer.Web
pnpm install --frozen-lockfile
pnpm run assets:build
cd ../..
```

### 5. .NET solution'ını hazırlayın

```bash
dotnet restore AssetValueAnalyzer.sln
dotnet build AssetValueAnalyzer.sln --no-restore
```

### 6. Web'i çalıştırın

Bir terminalde:

```bash
dotnet run --project src/AssetValueAnalyzer.Web --launch-profile http
```

Web adresi: `http://localhost:5271`.

Elle migration komutu çalıştırmanız gerekmez. Web başlangıçta 2. adımda
kaydedilen connection string ile mevcut MSSQL'e bağlanır ve bekleyen EF Core
migration'larını otomatik uygular. Veritabanı mevcut değilse SQL hesabının
veritabanı oluşturma yetkisi olmalıdır. Veritabanı yönetici tarafından önceden
oluşturulduysa hesabın bu veritabanında tablo ve index oluşturma yetkisi yeterlidir.

`Login failed` hatası kullanıcı/parola veya SQL erişimi sorunudur. `CREATE DATABASE
denied` ya da `CREATE TABLE permission denied` hatası alınırsa MSSQL yöneticisinin
kullanıcı yetkilerini düzeltmesi gerekir. Ayar düzeltildikten sonra aynı
`dotnet run` komutu yeniden çalıştırılır.

Migration tamamlandıktan sonra Web eksik Finmaks kur verilerini uzak MSSQL'e
aktarır. Bu yüzden sunucunun erişilebilir ve Finmaks API anahtarının doğru olması
gerekir.

Web health kontrolünü ikinci bir terminalden yapın:

```bash
curl http://localhost:5271/health
```

### 7. API'yi çalıştırın

Web çalışmaya devam ederken ayrı bir terminalde:

```bash
dotnet run --project src/AssetValueAnalyzer.Api --launch-profile http
```

Manuel API adresi: `http://localhost:5076`.

```bash
curl http://localhost:5076/health
curl "http://localhost:5076/api/exchange-rates/latest?baseCurrencyCode=1&foreignCurrencyCode=56&limit=1"
```

Web ve API'yi durdurmak için çalıştıkları terminallerde `Ctrl+C` kullanın. Uzak
MSSQL'in durdurulması veya yedeklenmesi o sunucunun yöneticisinin sorumluluğundadır.

## Kullanım

1. Ana sayfadan Aylık Varlık Verisi XLSX dosyasını yükleyin.
2. Yİ-ÜFE Endeks Verisi XLSX dosyasını yükleyin.
3. Rapor dönemini seçin veya dosyadaki ilk ve son ayın kullanılmasına izin verin.
4. Kontrol ekranından analizi oluşturun.
5. Sonuçları inceleyin veya raporu XLSX olarak indirin.

Sentetik örnek dosyalar web arayüzünden indirilebilir:

- `src/AssetValueAnalyzer.Web/wwwroot/samples/asset-values.xlsx`
- `src/AssetValueAnalyzer.Web/wwwroot/samples/producer-price-indices.xlsx`

Gerçek şirket dosyaları ve hassas finansal veriler repository'de tutulmaz.

## Kur API'si

API ayrı bir ASP.NET Core hostudur ve yalnız MSSQL'de bulunan kur kayıtlarını
okur. EF entity'leri doğrudan dışarı verilmez; response DTO kullanılır.

| Çalıştırma yöntemi | API base adresi |
| --- | --- |
| Yol 1 — Docker Compose | `http://localhost:5272` |
| Yol 2 veya Yol 3 — Manuel | `http://localhost:5076` |

| Method | Endpoint | Açıklama |
| --- | --- | --- |
| `GET` | `/health` | API ve veritabanı hazırlık kontrolü |
| `GET` | `/api/exchange-rates/latest` | En son veya belirli güne ait kur kayıtları |
| `GET` | `/api/exchange-rates` | Tarih aralığındaki kur kayıtları |

### Tarayıcıdan hızlı kontrol

Docker Compose çalışırken aşağıdaki adresler doğrudan tarayıcının adres çubuğuna
yazılabilir:

- Health: `http://localhost:5272/health`
- En güncel USD/TRY kaydı:
  `http://localhost:5272/api/exchange-rates/latest?baseCurrencyCode=1&foreignCurrencyCode=56&limit=1`
- Aralık 2021 USD/TRY kayıtları:
  `http://localhost:5272/api/exchange-rates?startDate=2021-12-01&endDate=2021-12-31&baseCurrencyCode=1&foreignCurrencyCode=56&limit=200`

`localhost` yalnız uygulamanın çalıştığı bilgisayarı ifade eder. API internete
deploy edilirse aynı path'lerin başındaki `http://localhost:5272` bölümü gerçek
sunucu adresiyle değiştirilir; örneğin
`https://example.com/api/exchange-rates/latest`.

### Güncel veya belirli gün sorgusu

```text
GET /api/exchange-rates/latest
```

Opsiyonel query parametreleri:

- `rateDate=YYYY-MM-DD`
- `baseCurrencyCode`
- `foreignCurrencyCode`
- `limit` — varsayılan `100`, izin verilen aralık `1–200`

Örnek USD/TRY sorgusu:

```bash
curl "http://localhost:5272/api/exchange-rates/latest?baseCurrencyCode=1&foreignCurrencyCode=56&limit=1"
```

### Tarih aralığı sorgusu

```text
GET /api/exchange-rates
```

- `startDate=YYYY-MM-DD` ve `endDate=YYYY-MM-DD` birlikte zorunludur.
- Para kodları ve `limit` opsiyoneldir.

```bash
curl "http://localhost:5272/api/exchange-rates?startDate=2021-12-01&endDate=2021-12-31&baseCurrencyCode=1&foreignCurrencyCode=56&limit=200"
```

Başarılı cevaplar JSON dizi döndürür. Geçersiz sorgular `400
ValidationProblemDetails`, eşleşmeyen sorgular `404 ProblemDetails` döndürür.

## Testler

Tüm testleri çalıştırmak için:

```bash
dotnet test AssetValueAnalyzer.sln --no-restore
```

Yalnız API ve API startup testleri:

```bash
dotnet test tests/AssetValueAnalyzer.IntegrationTests/AssetValueAnalyzer.IntegrationTests.csproj \
  --no-restore \
  --filter "FullyQualifiedName~ExchangeRatesApiTests|FullyQualifiedName~DatabaseStartupHostTests"
```

Son doğrulanan durum:

- Unit: `44/44`
- Integration: `110/110`
- Toplam: `154/154`
- API odaklı integration testleri: `13/13`
- Build: `0` hata, `0` uyarı

Test ortamı gerçek Finmaks servisine, production MSSQL'e veya çalışan Hangfire
kuyruğuna bağlanmaz. Dış bağımlılıklar kontrollü test double'larıyla değiştirilir.

## Temel iş kuralları

- Import formatı yalnız şirketin sabit şablonlarıyla uyumlu `.xlsx` dosyalarıdır.
- Yüklenen finansal dosyalar MSSQL'e yazılmaz; iki saatlik kullanıcı session'ında tutulur.
- MSSQL'de Finmaks `ExchangeRates` listesindeki bütün para çiftleriyle birlikte
  `BaseCurrencyCode`, `ForeignCurrencyCode`, `ChangeRate`, `ExchangeRate`,
  `CashChangeRate`, `CashExchangeRate`, `CentralBankChangeRate`,
  `CentralBankExchangeRate`, `CrossRate` ve `CurrentDate` alanları tutulur.
- Finmaks response header'ındaki işlem metadata'sı saklanmaz; kaydın alınma zamanı
  ayrıca `RetrievedAtUtc` olarak tutulur.
- USD/TRY için `BaseCurrencyCode = 1`, `ForeignCurrencyCode = 56` ve
  `CashChangeRate` kullanılır.
- Aylık kur, ayın son hafta gününden başlayarak en fazla 10 takvim günü geriye
  aranır.
- Finansal hesaplamalarda `decimal` kullanılır; yuvarlama presentation katmanında yapılır.
- Aynı para çifti ve gün için kur senkronizasyonu idempotent çalışır.

## Solution yapısı

```text
src/
├── AssetValueAnalyzer.Domain
├── AssetValueAnalyzer.Application
├── AssetValueAnalyzer.Infrastructure
├── AssetValueAnalyzer.Web
└── AssetValueAnalyzer.Api

tests/
├── AssetValueAnalyzer.UnitTests
└── AssetValueAnalyzer.IntegrationTests
```

Bağımlılık yönü:

```text
Domain <- Application <- Web / Api
              ^
              |
        Infrastructure
```

## Bilinen sınırlar

- Authentication ve authorization şartnamede istenmediği için yoktur.
- Session in-memory olduğu için Web yeniden başlatıldığında taslak rapor kaybolur.
- Çoklu Web instance'ı için distributed session store yapılandırılmamıştır.
- Public deployment zorunlu kapsamda değildir; HTTPS dış reverse proxy veya
  platform ingress katmanında sonlandırılmalıdır.
- Swagger/OpenAPI UI eklenmemiştir; API sözleşmesi bu README ve integration
  testlerinde belgelenmiştir.
