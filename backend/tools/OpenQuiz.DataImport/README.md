# OpenQuiz DataImport

Firestore'daki mevcut OpenQuiz verilerini MSSQL veritabanına aktaran konsol uygulaması.

## Gereksinimler

- .NET 10 SDK
- Firestore projesine erişim sağlayan **servis hesabı JSON** dosyası
- Çalışır durumda MSSQL veritabanı (migrations otomatik uygulanır)

## Taşınan Veriler

| Kaynak (Firestore)         | Hedef (MSSQL)                |
|----------------------------|------------------------------|
| `authorizedUsers/{email}`  | `Users` (CanCreate = true)   |
| `registeredUsers/{uid}`    | `Users`                      |
| `polls/{pollId}`           | `Polls`, `Questions`, `Options` |
| `polls/{pollId}/votes/*`   | `Votes`, `OpenAnswers`       |
| `scores/{userName}`        | `Scores` (global, PollId=null) |

## Kullanım

```bash
dotnet run --project tools/OpenQuiz.DataImport -- \
    --FirebaseProjectId=openquiz-c9a0c \
    --GoogleCredentials=/path/to/service-account.json \
    --ConnectionString="Server=localhost;Database=OpenQuiz;User Id=sa;Password=YourPass123!;TrustServerCertificate=True"
```

### Parametreler

| Parametre            | Zorunlu | Varsayılan       | Açıklama |
|----------------------|---------|------------------|----------|
| `FirebaseProjectId`  | ✅      | —                | Firebase proje ID'si |
| `GoogleCredentials`  | ❌      | `GOOGLE_APPLICATION_CREDENTIALS` env | Servis hesabı JSON dosya yolu |
| `ConnectionString`   | ✅      | —                | MSSQL bağlantı dizesi |
| `AppId`              | ❌      | `quiz-master-pro` | Firestore artifact yolu (`artifacts/{AppId}/public/data`) |
| `DryRun`             | ❌      | `false`          | `true` ise sadece okur, veritabanına yazmaz |

Parametreler hem komut satırı argümanı (`--Key=Value`) hem de ortam değişkeni olarak verilebilir:

```bash
export FIREBASE_PROJECT_ID=openquiz-c9a0c
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/sa.json
export OPENQUIZ_CONNECTION="Server=localhost;..."
dotnet run --project tools/OpenQuiz.DataImport
```

## Dry Run

Veritabanına yazmadan kaç kayıt aktarılacağını görmek için:

```bash
dotnet run --project tools/OpenQuiz.DataImport -- \
    --FirebaseProjectId=openquiz-c9a0c \
    --ConnectionString="..." \
    --DryRun=true
```

## İdempotency (Tekrarlı Çalıştırma)

Araç tekrar çalıştırıldığında:

- **Users**: Email bazında kontrol; varsa atlar, `CanCreate`/`DisplayName` günceller.
- **Polls**: Başlık + oluşturucu bazında kontrol; varsa atlar.
- **Votes**: `QuestionId + UserName` bazında kontrol; varsa atlar.
- **Scores**: `UserName` bazında kontrol; varsa puanı günceller.

## Çıkış Kodları

| Kod | Anlam |
|-----|-------|
| `0` | Başarılı |
| `1` | Yapılandırma hatası veya kritik hata |
| `2` | Import tamamlandı ama bazı kayıtlarda hata oluştu |

## Çıktı Örneği

```
───────────────── OpenQuiz DataImport ──────────────────
┌───────────────────┬─────────────────────────────┐
│ Parameter         │ Value                       │
├───────────────────┼─────────────────────────────┤
│ Firebase project  │ openquiz-c9a0c              │
│ App id            │ quiz-master-pro             │
│ Dry-run           │ NO                          │
└───────────────────┴─────────────────────────────┘

✓ Connected to Firestore
✓ Database migrations applied

1/4 Importing users...
  Users: 12 imported, 3 skipped, 0 errors
2/4 Importing polls...
  Polls: 8 imported, 0 skipped, 0 errors
3/4 Importing votes...
  Votes: 245 imported, 12 skipped, 0 errors
4/4 Importing scores...
  Scores: 30 imported, 0 skipped, 0 errors

───────────────────── Summary ─────────────────────────
┌────────┬──────────┬─────────┬────────┬───────┐
│ Entity │ Imported │ Skipped │ Errors │ Total │
├────────┼──────────┼─────────┼────────┼───────┤
│ Users  │       12 │       3 │      0 │    15 │
│ Polls  │        8 │       0 │      0 │     8 │
│ Votes  │      245 │      12 │      0 │   257 │
│ Scores │       30 │       0 │      0 │    30 │
│        │          │         │        │       │
│ Total  │      295 │      15 │      0 │   310 │
└────────┴──────────┴─────────┴────────┴───────┘

✓ Import complete!
```
