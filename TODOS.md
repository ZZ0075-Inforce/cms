# TODOS

## 資安待辦（security）

來源：2026-07-17 的 `/cso` 全專案稽核（daily mode，8/10 信心門檻）。
完整報告與每一項的驗證紀錄在 `.gstack/security-reports/2026-07-17-123346.json`（未進版控）。

該次稽核掃描 13 個候選、報告 1 項（Swagger 匿名暴露，**已修復**，見 commit `0deeaf0`）。
以下 11 項是**沒有通過 8/10 信心門檻**而未列為正式發現的項目，加上兩項維護建議。

**先看懂這張表怎麼讀。** 括號裡的分數是**信心**（「我多確定這是真的」），不是嚴重性。
兩者是分開的：TLS 那項信心只有 4/10 卻排第一，因為它一旦成真後果最大；
`db_datareader` 那類則是信心高但影響小。**別用分數排優先序，用下面的順序。**

驗證原則：每一項都附了可引用的 file:line。動手前先確認那行還在——這份清單寫於
2026-07-17，程式碼會變。

---

### P1 — 正式環境沒有 TLS，密碼與 JWT 明文過網路

- **證據**：`deploy/setup-iis.ps1` 的 `New-Website` 無 SSL 繫結；全 `deploy/` 目錄零 TLS 設定
  （唯二的文字命中是 MSI 下載網址）。`src/CMS.API/Program.cs` 無 `UseHsts()`、無 `UseHttpsRedirection()`。
- **後果**：`POST /api/Auth/login` 帶明文密碼，回應帶可重放的 JWT，之後每個請求都重複帶著它。
  任何在路徑上的人（同網段、ARP/DNS 欺騙、惡意 Wi-Fi、被入侵的交換器）被動就能收走帳密。
  登入限流（10 次 / 5 分鐘）對一個直接從線路上讀到密碼的攻擊者完全無效。
- **為何沒列為正式發現**：稽核評 4/10 —— 屬 missing hardening，且需要攻擊者已在區網內。
  但這是整份清單上後果最大的一項。
- **動手前要知道**：這需要一張憑證，腳本生不出來。要先決定憑證來源（內部 CA？Let's Encrypt？
  公司既有 wildcard？）。加 TLS 時**繫結與 redirect 要一起加** —— 只加其中一半會做出
  一個 307 到沒人在聽的埠的 API。
- **不要**用環境變數當替代品。`DEPLOY-IIS.md` 有一段歷史教訓：舊註解宣稱環境旗標會控制
  HSTS/redirect，那段程式碼從來不存在，而那個假設攔住了大家好幾個月。

### P2 — 正式資料庫可能還有舊的「無鹽」SHA-256 密碼雜湊

- **證據**：`src/CMS.API/Infrastructure/PasswordHasher.cs:49-51` 的 `Verify()` 仍接受
  2026-07-17 之前的裸 64 字元 hex 格式；`NeedsRehash()` 會在下次成功登入時升級。
- **後果**：只有**從未登入過**的帳號會保留舊雜湊。這些帳號都是用 SysConfig 的預設密碼建立的，
  舊格式無鹽 → 它們的雜湊 byte-identical，掃一眼欄位就能認出「所有從沒登入過的帳號」。
- **為何沒列為正式發現**：評 4/10。要利用它得先有 DB 讀取權，而有 DB 讀取權的人本來就能
  直接讀到 `SysConfig.appConfig.defaultPassword` 的明文，所以無鹽雜湊實際上沒多給攻擊者什麼。
- **該做的事**：確認遷移是否已完成。跑一句
  `SELECT COUNT(*) FROM dbo.AppUser WHERE LEN(PasswordHash) = 64 AND PasswordHash NOT LIKE '%$%'`。
  歸零後就可以把 `PasswordHasher` 的 legacy 路徑整段刪掉（該檔 :16-19 的註解已寫明這個條件）。

### P2 — JWT 沒有撤銷機制，停用/刪除/重設密碼最多 24 小時後才生效

- **證據**：`src/CMS.API/Infrastructure/ConfigureJwtBearerOptions.cs:27-36` 的
  `TokenValidationParameters` 只驗簽章與 `exp`。全 `src/CMS.API` 沒有 `JwtBearerEvents`、
  `OnTokenValidated`、denylist 或 security stamp。`JwtTokenGenerator.cs:24` 的
  `Lifetime = TimeSpan.FromHours(24)`（加 5 分鐘預設 clock skew → 實際 24h05m）。
  `IsActive = 1` 只在 `AuthRepository.cs:36` 的登入路徑檢查。
- **後果**：管理員按下「停用」或「刪除」時相信權限已經斷了，但沒有。刪除的帳號連 DB 資料列都
  不存在了，token 照樣通行。最尖銳的是重設密碼——管理員之所以重設，往往正是因為懷疑帳號被盜，
  而攻擊者手上的 token 毫髮無傷。角色也是凍結的：被降級的管理員在 24 小時內仍持有 Admin claim。
- **為何沒列為正式發現**：評 4/10 —— 這是無狀態 JWT 的教科書行為，缺的是沒蓋的功能而非壞掉的
  程式，且沒有跨越任何持有者本來就不該有的權限邊界。
- **現在唯一的即時除權手段**：輪換 `SysConfig.appConfig.symmetricSecurityKey`，這會一次作廢
  **所有人**的 token。目前沒有任何文件告訴維運人員這件事——至少該把它寫進 DEPLOY-IIS.md。
- **若要真的修**：在 `OnTokenValidated` 裡比對 token 的 `iat` 與 `AppUser.PasswordUpdatedTime`
  （這個欄位已經存在，見 `AuthRepository.cs:233`）。成本很低。或把 lifetime 從 24h 降到 8h
  （一個班次），把窗口壓進工作日內。
- **會升級為正式發現的情況**：這個 CMS 若對外網開放，或使用者含約聘/臨時人員，
  或需符合 SOC 2 CC6.2 / ISO 27001 A.9.2.6（「及時移除存取權」是可稽核要求）。

### P3 — `stdoutLogEnabled="true"` 把完整堆疊與 SQL 寫進磁碟

- **證據**：`deploy/CMS.API/web.config.template:20`。`deploy/deploy.ps1` 每次部署都會蓋這個模板。
- **後果**：`GlobalExceptionMiddleware.cs:29` 會在回傳安全的 500 之前記錄完整例外，
  所以每個堆疊、每個 `SqlException`（含 SQL 文字）、連線細節都落到
  `C:\VHome\CMS\API\logs\stdout_*.log`。且無輪替，每次程序啟動開一個新檔。
- **範圍要講清楚**：**不是** web 可讀取的——ANCM 接管所有請求，app 也沒註冊
  `UseStaticFiles`，所以 `GET /logs/stdout_*.log` 會 404。這是磁碟／內部人員／
  入侵後的暴露面，不是遠端讀取。
- **修法**：改成 `stdoutLogEnabled="false"`。logger 的輸出本來就會經 ANCM 進 Windows 事件記錄檔；
  真的要 debug 時再用 `<handlerSettings>` 的 debug log。MS 官方文件也說 stdout log 只該用於
  疑難排解，不該常駐在正式環境。

### P3 — `setup-iis.ps1` 下載三個安裝程式並以 Administrator 執行，無簽章驗證

- **證據**：`deploy/setup-iis.ps1:146` 下載、:151-153 執行；三個網址在 :168、:177、:183，
  其中 `https://aka.ms/dotnet/9.0/dotnet-hosting-win.exe` 是轉址器。
- **公平地說**：這**不是** `iwr | iex`。TLS 1.2 已在 :145 釘選，來源都是 Microsoft 的 HTTPS 網域，
  全 repo 沒有 `-ExecutionPolicy Bypass`。比常見的糟糕寫法安全得多。
- **殘留缺口**：那些網址回傳什麼就以 Administrator `/quiet` 執行什麼。TLS 攔截中介盒
  （企業網路常見，而這正是地端部署套件）、裝了流氓根憑證的機器被 DNS 劫持、或 `aka.ms`
  轉址目標被入侵，都會變成 IIS 主機上的 SYSTEM 權限執行。
- **修法**：`Start-Process` 前驗簽章。
  ```powershell
  $sig = Get-AuthenticodeSignature $file
  if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
      throw "$Name: bad signature"
  }
  ```

### P3 — `GET /api/lookups/app-users` 對任何已登入者公開完整使用者名冊

- **證據**：`src/CMS.API/Controllers/LookupsController.cs:13` 無任何授權屬性
  （`AppUsersController.cs:28` 則有 `[Authorize(Roles = "Admin")]`）。
  `LookupRepository.cs:12-14` 的 SQL 是 `SELECT u.UserId, u.UserName, u.IsActive FROM dbo.AppUser u`，
  無 `WHERE`。
- **為何沒列為正式發現**：評 3/10。洩漏的是 Admin-gated 模型的**嚴格子集**——不含 `Pkid`、
  `PasswordUpdatedTime`、`RoleCount`、`RoleIds`。**角色對應完全沒洩**，所以攻擊者連該鎖定
  哪個同事提權都判斷不了。而呼叫者本來就是已登入的內部員工，手上就有公司通訊錄。
- **但這個閘門確實不一致，而且是白給的**：這個 lookup 的唯一消費者是
  `app-role-form.ts:75` 和 `app-role-detail.ts:46`，而 `AppRolesController.cs:24` 本身就是
  Admin-only。非管理員沒有任何正當理由呼叫它。
- **修法**：`[Authorize(Roles = "Admin")]` 加在 `GetAppUsers`（和 `GetAppRoles`）上。一行。
  **不要**整個 class 加——其他 lookup（publish-statuses、course-groups…）是 Course 表單要用的，
  對已登入者開放是刻意設計。

### P3 — `deploy.ps1` 的連線字串用 `TrustServerCertificate=True` 搭 `Encrypt=True`

- **證據**：`deploy/deploy.ps1:68`。語意是「加密但不驗證憑證」——依定義可被 MITM。
- **為何沒列為正式發現**：評 3/10，**潛在而非現行**。`Server=.\SQLEXPRESS` 是 loopback，
  沒有網路路徑可以 MITM。而且對 SQLEXPRESS 的自簽憑證來說，`TrustServerCertificate=True`
  其實是**正確且必要**的設定——拿掉它連線會直接失敗。
- **什麼時候會變成真問題**：`DEPLOY-IIS.md:85-89` 與 `setup-iis.ps1:302-306` 都記載遠端 SQL
  是支援的拓撲。一旦 `Server=` 指向機器外，攻擊者就能用自簽憑證接管 TLS 連線，
  讀寫每個查詢——包括存著 **JWT 簽章金鑰**的 `SysConfig` 資料列，那等於完整的認證繞過。
- **該做的事**：現在只需在 `deploy.ps1:68` 加一行警告註解。真的改用遠端 SQL 時，
  在 SQL 執行個體裝一張受信任憑證並拿掉 `TrustServerCertificate=True`。

### P3 — 確認對話框把 DB 內容插進 `[innerHTML]`（12 處）

- **證據**：`course-group-list.ts:129`、`course-list.ts:175`、`partner-list.ts:119` 等 12 處，
  把 DB 字串插進含 `<b>` 的 HTML 樣板；PrimeNG 的 ConfirmDialog 用
  `[innerHTML]="option('message')"` 渲染。
- **為何沒列為正式發現**：評 3/10。Angular 的 `[innerHTML]` **屬性繫結**會過 `DomSanitizer`，
  而全 `src/CMS.NG` 有**零個** `bypassSecurityTrust*`。這是框架的預設安全路徑，不是逃生艙
  （既有慣例：Angular 預設 XSS-safe，只報逃生艙）。
- **能存活 sanitizer 的**：靜態標記、`class` 屬性、`<a href="https://…">`、`<img src="https://…">`。
  **不能**：`<script>`、`on*` 事件處理器、`javascript:` URL。
  天花板是「粗體字 + 外連釣魚 + 圖片信標（洩漏管理員的 IP/UA）」，不是 XSS。
- **不過跨權限跳躍是真的**：`CoursesController` / `CourseGroupsController` / `PartnersController`
  都沒有角色閘門，所以低權限使用者能種下標記，之後在管理員的對話框裡渲染。
- **要做的話**：對插入的欄位加一個 `escapeHtml()` helper。**當作衛生習慣做，不是當作漏洞修**。
  注意 `accept:` 是在 TypeScript 綁的，注入的標記改不了對話框**做什麼**。

### P4 — `GET /api/rowaudit` 沒有角色閘門

- **證據**：`src/CMS.API/Controllers/RowAuditController.cs:14` 無授權屬性，`tableName` 由查詢字串任意指定。
- **為何沒列為正式發現**：評 2/10。`RowAuditRepository.cs:16` 只 `SELECT [DateTime], UserName,
  ActionType, ActionDesc`。Update 類型的 `ActionDesc` 只存變更欄位**名稱**不存值
  （`RowAuditWriter.cs:112-118` 的 `.Select(p => p.Name)`），且 `PasswordHash` 根本不在稽核快照裡
  （`AppUserRepository.cs:222`）。攻下它的全部收穫是「某天某管理員改了 AppUser pkid 5 的
  UserName,IsActive 欄位」，而且你得先知道 pkid。
- **要做的話**：對敏感 tableName 加 `[Authorize(Roles = "Admin")]`，或用白名單限制可查表名。

### P4 — 完全沒有登入事件記錄

- **證據**：全 app 唯一的 logger 呼叫是 `GlobalExceptionMiddleware.cs:29`。
  成功登入、失敗登入、授權失敗都不留痕跡。
- **為何沒列為正式發現**：CSO 規則明確排除「缺少稽核紀錄不算漏洞」。
- **但值得做**：出事時你會希望有這個。至少記登入失敗（含來源 IP）與 429 觸發，
  它們是密碼噴灑攻擊的訊號。注意**不要**記密碼本身。

### P4 — `appsettings.Development.json` 會被部署到正式資料夾並載入

- **證據**：`deploy.ps1:170` 的 `dotnet publish` 會納入 `appsettings*.json`，:225 整包複製。
- **影響**：接近零。該檔只有記錄層級，無秘密。而且 commit `0deeaf0` 之後環境已改為 Production，
  它連載入都不會載入了。
- **衛生**：在 csproj 加 `<Content Remove="appsettings.Development.json" CopyToPublishDirectory="Never" />`。

### P4 — 沒有秘密掃描防護檔

- **現況**：`.gitleaks.toml` 與 `.secretlintrc` 都不存在。
- **背景**：2026-07-17 的全歷史秘密考古是**零命中**——沒有任何需要輪換的憑證，
  簽章金鑰從未進版控（`database/*.sql` 只有 DDL，連一個 INSERT 都沒有）。
  目前是乾淨的，防護檔是為了讓它**保持**乾淨。
- **要做的話**：加 `.gitleaks.toml`，並把測試常數
  （`AdminAuthTestFactory.cs:29`、`JwtAuthTestFactory.cs:21` 等的 `*-signing-key-*`）加進 allowlist，
  否則它們每次都會誤報。

### P4 — .NET SDK 停在 9.0.314（修補版 9.0.316）

- **現況**：`global.json` 釘 9.0.314 搭 `rollForward: latestPatch`；本機實際解析到 9.0.314。
- **好消息**：**Runtime 已是修補版**。ASP.NET Core 9.0.18 已安裝，`net9.0` 的 roll-forward 會用它，
  而 `setup-iis.ps1` 抓的 hosting bundle 是 always-latest ⇒ 正式環境跑的東西是修好的。
- **為何沒列為正式發現**：兩個相關的 SDK CVE 都打不到這個專案。
  CVE-2026-50526 是容器映像建置污染（本專案零容器）；
  CVE-2026-45490 是 `dotnet workload` 具名管道的本機提權（需要有人已在開發機上）。
  CVE-2026-50524（TLS DoS）屬 DoS 排除項，且 runtime 已修補。
- **要做的話**：裝 SDK 9.0.316+ 並更新 `global.json`。純維護，不急。

---

## 品質待辦（QA — Course PDF）

來源：2026-07-17 對 `http://localhost:4200/courses/:id/print` 的 `/qa`（Standard tier，以帳號
`test` 登入）。完整報告與截圖在 `.gstack/qa-reports/qa-report-course-pdf-2026-07-17.md`（未進版控）。

這次順帶還掉了這個功能出貨時掛的兩筆債：列印 CSS **已在 Chromium 實際印出來看過**，
欄位白名單**已用 8 門真實課程驗證**，無任何內部欄位外洩。詳見報告的「Verified working」表。

**兩項都不在 `fix/cso-swagger-exposure-and-audit-gaps` 上。** `course-print` 由 `7c99a9a` 撰寫、
`8cf17fa` 併入 develop。要動請**從 develop 開分支**，別讓版面改動混進資安 PR。

### P3 — 客戶文件的最後一頁只有一個 QR code（約 70% 的課程）

- **證據**：`.sheet-footer`（QR 卡 + 提示）實測高 **314.6px**（QR 圖 180×180、卡片 275px），
  約佔 A4 可用高度的 31%。圖片無法跨頁切割，上一頁剩餘空間小於它時整塊被推到新頁。
  樣式在 `src/CMS.NG/src/app/features/courses/course-print/course-print.scss:90-101`。
- **實測 8 門課的末頁字元數**（去空白後；QR 卡本身約佔 37 字元）：
  62=37、66=33、586=40、633=41、674=74 → 末頁僅剩 QR；618=98 為邊緣；
  348=412、349=275 → 正常。**5–6/8（約 70%）**。
- **後果**：寄給客戶或印出來的課程資訊，最後一頁是 95% 空白的紙。浪費，且讀起來像沒做完。
- **動手前要知道**：**這不是單純的 CSS 修復，是版面決策。** 把 QR 從 180px 縮到 100px 只省約
  80px，但缺口是 314px 對上約 230px 的剩餘空間 —— 改完仍會依課程內容長度時好時壞。
  真正要決定的是「客戶文件的結尾該長怎樣」（QR 縮小？移到內容頁右下？接受獨立一頁？），
  這需要業務／設計的意見，不該由工程單方面決定。

### P4 — `course-print.ts` 的註解宣稱白名單是唯一出口，與實際不符

- **證據**：`src/CMS.NG/src/app/features/courses/course-print/course-print.ts:22` 寫著
  「What ships out is decided by the two lists below, and nowhere else」。
  但 `course-print.html:44-64` 另外直接從樣板渲染兩段，未經 `summaryRows()` / `contentRows()`：
  `相關認證` ← `data.certificationLabels`、`適合職務` ← `data.jobCategoryLabels`。
  兩者都確實出現在產出的 PDF 上。
- **後果**：**今天沒有外洩。** 這兩者是 n-n 對照表的標籤，新增 `Course` 欄位仍然到不了它們，
  而且樣板是逐項列舉而非展開，安全性質本身成立。問題在於這句註解是下一個人稽核
  「什麼會到客戶手上」時讀的地圖 —— 它指向兩份清單，實際上有四個來源。
- **修法**：把註解改成列出全部四個來源。純註解修改，零行為風險。

---

## 品質待辦（QA — 全專案掃描）

來源：2026-07-17 的全專案 `/qa`（Standard tier，以帳號 `test` 登入），涵蓋 7 個功能與認證流程。
完整報告在 `.gstack/qa-reports/qa-report-full-app-2026-07-17.md`（未進版控）。

**結果：全部只有 1 項缺陷（Low）。** 所有頁面零 console 錯誤，驗證閘門處處守住，
認證邊界完好。**順帶把 `ec8902b` 的修復第一次用真瀏覽器驗過** —— 該 commit 只有
「290/290 單元測試 + ng build」背書，但那個 bug 的本質是「紅字沒變紅」，測試看不到顏色。
實測課程群組 260（底下 4 門課）的刪除對話框，警告確實是 `rgb(220,38,38)` / `font-weight:600`，
`.confirm-warning` 全域 class 有活過 sanitizer。**修復有效。**

### P4 — 上稿看板的 PromoCode 查詢，一次點擊送出兩個請求

- **證據**：`src/CMS.NG/src/app/features/featured-promo-items/featured-promo-item-form/featured-promo-item-form.html:11`
  的 input 綁了 `(blur)="lookup()"`，:18 的查詢鈕綁了 `(onClick)="lookup()"`。
  使用者點查詢鈕時焦點離開輸入框 → blur 先呼叫一次 `lookup()`，click 再呼叫一次。
- **實測拆解**（每個綁定各貢獻一個請求，是驗證不是推論）：
  真實點擊（焦點在輸入框）= 2；JS `.click()`（不移動焦點，不觸發 blur）= 1；
  單獨 `blur()` = 1；`blur()` 後再 click = 2。
- **後果**：小。這是等冪的 GET 且兩次帶同樣的代碼，不可能競態成錯誤結果。
  代價是每次查詢多一趟往返，代碼查不到時 console 多一行 404。
- **修法**：拿掉 `(blur)` 綁定，或在 `lookup()` 內對同一代碼的進行中請求加防護
  —— `lookingUp()` signal 已經存在，一行就夠。

### 觀察（未列為缺陷）

- **約 1280px 以下頁面會水平捲動**：1013px 時 `scrollWidth` 1173 vs `clientWidth` 998，
  看板的 Description 欄跑到畫面外。**內容沒被切掉**，捲動就看得到，1280px 以上完全無溢出。
  對內部管理後台可接受，記著以備日後真的要支援更窄的螢幕。
- **`launchSettings.json` 有明文開發密碼**（`Password=LocalDev#Cms2026`）且該檔進版控。
  本機拋棄式容器算常見做法，但這個 repo 剛做完資安稽核，值得有意識地決定而不是放它飄著。
  這是隨 Docker 工作進來的，非 QA 產生。

### 這次沒測到的（別把分數讀得比實際大）

- **無障礙**：完全沒測。沒有鍵盤巡覽、沒有螢幕報讀、沒有對比度稽核。
- **效能**：沒測量。
- **手機／平板版面**：沒測，視為桌面管理後台不在範圍。
- **寫入的完整往返**：只在 `/partners/new` 推過驗證閘門，全程沒有真的新增／修改／刪除任何資料。
  唯一動到的是看板兩次 slot 移動，已還原並驗證回原始順序。
- **各實體的表單深度**：只有 partners 測透；其餘五個只驗了列表、明細與 badge。

---

## 已完成（2026-07-17）

- ~~Swagger 在區網匿名公開整個 API 表面~~ — 已修 (`0deeaf0`)，Production/Development 兩邊實測驗證
- ~~管理員重設密碼未留稽核紀錄~~ — 已修 (`e47ab5b`)
- ~~串聯刪除警告被 sanitizer 剝除而靜默失效~~ — 已修 (`ec8902b`)

> **這份清單不能取代專業資安稽核。** 來源是 AI 輔助掃描，會捕捉常見漏洞模式，但不完整、
> 不保證，也不能取代合格資安廠商。對於處理敏感資料、付款或個資的正式系統，
> 請聘請專業滲透測試廠商。
