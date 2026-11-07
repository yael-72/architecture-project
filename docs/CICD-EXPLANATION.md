# CI/CD Pipeline — הסבר מלא

## מה זה CI/CD ולמה זה חשוב?

**CI (Continuous Integration)** = אינטגרציה מתמשכת  
בכל פעם שדוחפת קוד ל-GitHub, מערכת אוטומטית בודקת שהקוד תקין — בונה אותו ומריצה טסטים. אם טסט נכשל, ה-pipeline "נופל" ומונע merge של קוד שבור.

**CD (Continuous Delivery)** = אספקה מתמשכת  
אחרי שה-CI עבר בהצלחה, המערכת בונה Docker images מוכנים להפצה ומתייגת אותם עם מזהה ייחודי (commit SHA).

**הערך הפרקטי:**
- קוד שבור **לא יכול** להיכנס ל-master
- כל גרסה מתועדת עם tag ייחודי (ניתן לחזור לכל גרסה)
- Docker images מוכנים תמיד להפצה לסביבת production

---

## הארכיטקטורה של ה-Pipeline

```
git push → GitHub Actions מתעורר
               ↓
    ┌──────────────────────┐
    │  Job 1: test          │  ← מריץ 6 טסטים של OrderService
    │  dotnet test          │     אם נכשל — עוצר הכל
    └──────────┬───────────┘
               ↓ (רק אם עבר)
    ┌──────────────────────┐
    │  Job 2: build         │  ← בונה את כל 7 השירותים
    │  dotnet build Release │     מוודא שאין שגיאות compile
    └──────────┬───────────┘
               ↓ (רק אם עבר + רק על push, לא PR)
    ┌──────────────────────────────────┐
    │  Job 3: docker (×7 במקביל)       │
    │  docker build + push             │
    │  tag: abc1234 + latest           │
    └──────────────────────────────────┘
               ↓
    ghcr.io/saradev841/orderservice:abc1234 ✅
    ghcr.io/saradev841/apigateway:abc1234   ✅
    ... (כל 7 השירותים)
```

---

## קובץ ה-Workflow: `.github/workflows/ci.yml`

### Trigger — מתי מופעל?
```yaml
on:
  push:
    branches: [ master, main ]    # כל push ל-master
  pull_request:
    branches: [ master, main ]    # כל PR
```

### Job 1 — Unit Tests
```yaml
- name: Run tests
  run: dotnet test OrderService.Tests/OrderService.Tests.csproj
```
**מה קורה:** מריץ 6 טסטים של OrderService. אם אחד נכשל — כל ה-pipeline נעצר.

### Job 2 — Build כל השירותים
```yaml
for proj in UserAuthService ProductCatalogService OrderService ...; do
    dotnet build $proj/$proj.csproj -c Release
done
```
**מה קורה:** מבנה את כל 7 השירותים ב-Release mode.

### Job 3 — Docker Images
```yaml
- name: Build and push
  uses: docker/build-push-action@v5
  with:
    push: true
    tags: |
      ghcr.io/saradev841/.../orderservice:abc1234
      ghcr.io/saradev841/.../orderservice:latest
```
**מה קורה:** בונה Docker image לכל שירות ומעלה ל-GitHub Container Registry עם:
- `abc1234` = 7 תווים ראשונים של commit SHA (מזהה ייחודי לכל גרסה)
- `latest` = תמיד מצביע על הגרסה האחרונה

---

## הטסטים — `OrderService.Tests`

נכתבו 6 טסטים ל-OrderService עם **xUnit** ו-**Moq**:

| טסט | מה בודק |
|---|---|
| `CreateOrder_WhenUserNotFound_ThrowsArgumentException` | יוזר לא קיים → שגיאה |
| `CreateOrder_WhenProductNotFound_ThrowsArgumentException` | מוצר לא קיים → שגיאה |
| `CreateOrder_HappyPath_ReturnsPendingOrderAndPublishesEvent` | הזמנה תקינה → סטטוס Pending + פרסום `order.placed` |
| `GetOrderById_WhenExists_ReturnsMappedDto` | הזמנה קיימת → מחזיר DTO |
| `GetOrderById_WhenNotFound_ReturnsNull` | הזמנה לא קיימת → null |
| `GetOrdersByUserId_ReturnsOnlyUserOrders` | מחזיר רק הזמנות של המשתמש הנכון |

**Moq** = ספריית Mock שמאפשרת לבדוק את ה-OrderService בבידוד מוחלט — ללא DB, ללא RabbitMQ, ללא HTTP. כל תלות חיצונית מוחלפת ב-"בובה" שמחזירה ערכים מוגדרים מראש.

---

## איך לראות את ה-Pipeline רץ?

### בדפדפן:
1. כנסי ל-https://github.com/SaraDev841/ArchitectureFinalProject/actions
2. תראי את כל ה-runs — ירוק ✅ = עבר, אדום ❌ = נכשל
3. לחצי על run ספציפי → תראי את ה-jobs ואת הלוגים

### ה-Badge ב-README:
```
[![CI](https://github.com/SaraDev841/ArchitectureFinalProject/actions/workflows/ci.yml/badge.svg)](...)
```
מציג **תמיד** את הסטטוס העדכני של ה-pipeline.

---

## הפעלה ידנית (Manual Trigger)

### אפשרות 1 — דרך GitHub UI:
1. כנסי ל-https://github.com/SaraDev841/ArchitectureFinalProject/actions
2. בחרי "CI — Build, Test & Push"
3. לחצי **"Run workflow"** → **"Run workflow"** (ירוק)

> כדי לאפשר זאת, צריך להוסיף `workflow_dispatch` ל-ci.yml (ראי למטה)

### אפשרות 2 — Push קוד ריק:
```bash
git commit --allow-empty -m "trigger CI"
git push origin master
```

### אפשרות 3 — הוספת `workflow_dispatch` (לאפשר כפתור ידני):
ב-`.github/workflows/ci.yml` תוסיפי:
```yaml
on:
  push:
    branches: [ master, main ]
  pull_request:
    branches: [ master, main ]
  workflow_dispatch:    # ← שורה זו מוסיפה כפתור ידני ב-GitHub UI
```

---

## מה להסביר למורה

### 1. "למה CI/CD חשוב בפרויקט המיקרו-שירותים שלך?"
> יש 7 שירותים שמתקשרים ביניהם. שינוי ב-OrderService יכול לשבור את ה-Saga בלי שתשימי לב. ה-CI מבטיח שכל שינוי עובר בדיקה אוטומטית **לפני** שהוא מגיע ל-master.

### 2. "מה ה-pipeline עושה?"
> שלושה שלבים: טסטים (fail-fast) → בניית כל השירותים → בניית Docker images עם commit SHA ודחיפה ל-registry. כל שלב תלוי בהצלחת הקודם.

### 3. "למה commit SHA ולא גרסה מספרית?"
> SHA מזהה **בדיוק** איזה קוד בנה את ה-image. אם יש bug בפרודקשן, ניתן לחפש `abc1234` ולמצוא מיד את ה-commit שגרם לו. גרסה מספרית (1.0.1) לא נותנת מידע על הקוד.

### 4. "איך טסט כושל חוסם merge?"
> Job 1 (test) חייב להצליח לפני Job 2 ו-3. GitHub מראה ❌ על ה-PR ומונע merge אם ה-check נכשל. ניתן להגדיר "Branch protection rules" שמחייבים ✅ לפני merge.

### 5. "מה זה Moq ולמה משתמשים בו?"
> Moq מאפשר לבדוק את ה-OrderService בלי להריץ DB אמיתי, RabbitMQ אמיתי, או HTTP calls. הטסט רץ תוך מילישניות (לא דקות), והוא deterministic — תמיד מחזיר אותה תוצאה.

---

## דמו מהיר למצגת

```bash
# 1. תראי שהטסטים עוברים מקומית
dotnet test OrderService.Tests/OrderService.Tests.csproj

# 2. תשני טסט כדי שיכשל
#    (שני "Confirmed" במקום "Pending" בשורה 95 של OrderServiceTests.cs)

# 3. Push
git add .; git commit -m "demo: failing test"; git push

# 4. תראי ב-GitHub Actions שה-pipeline נכשל ❌

# 5. תתקני חזרה ו-push שוב → ✅
```
