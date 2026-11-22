# Проверка окна AMFIBIA Studio через UI Automation: программа запускается и проходит сценарии сотрудников
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Mouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(80); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero); }
}
"@

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$script:fails = 0
$shots = Join-Path (Get-Location) 'shots'
New-Item -ItemType Directory -Force $shots | Out-Null
$nb = [char]0x00A0

function Check([string]$name, [bool]$ok, [string]$details = '') {
    if ($ok) { Write-Host "OK   $name  ->  $details" } else { Write-Host "FAIL $name  ->  $details"; $script:fails++ }
}
function Shot([string]$name) {
    Start-Sleep -Milliseconds 400
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $shots "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
function Cond($prop, $value) { New-Object System.Windows.Automation.PropertyCondition($prop, $value) }
function FindWindow([string]$title) { $AE::RootElement.FindFirst($TS::Children, (Cond $AE::NameProperty $title)) }
function WaitWindow([string]$title, [int]$seconds = 40) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        if ($script:proc.HasExited) { throw "Программа завершилась с кодом $($script:proc.ExitCode)" }
        $w = FindWindow $title
        if ($w) { return $w }
        Start-Sleep -Milliseconds 300
    }
    throw "Окно '$title' не появилось"
}
function Wait([scriptblock]$cond, [int]$seconds = 10) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) { try { if (& $cond) { return $true } } catch {} ; Start-Sleep -Milliseconds 250 }
    return $false
}
function El($parent, [string]$id) {
    $e = $null
    $ok = Wait { $script:tmp = $parent.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)); $script:tmp -ne $null } 6
    if (-not $ok) { throw "Элемент $id не найден" }
    return $script:tmp
}
function ByName($parent, [string]$name) {
    $ok = Wait { $script:tmp = $parent.FindFirst($TS::Descendants, (Cond $AE::NameProperty $name)); $script:tmp -ne $null } 6
    if (-not $ok) { throw "Элемент с текстом '$name' не найден" }
    return $script:tmp
}
function TextOf($parent, [string]$id) { (El $parent $id).Current.Name.Replace($nb, ' ') }
function SetValue($el, [string]$text) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text); Start-Sleep -Milliseconds 300 }
function Press($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700 }
function SelectItem($item) { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 700 }
function Toggle($el) { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 300 }
function ClickEl($el) {
    $r = $el.Current.BoundingRectangle
    [Mouse]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 900
}
function Items($el, $type) { $el.FindAll($TS::Descendants, (Cond $AE::ControlTypeProperty $type)) }
function ChooseInCombo($combo, [string]$text) {
    $ec = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $ec.Expand(); Start-Sleep -Milliseconds 500
    $item = $combo.FindFirst($TS::Descendants, (Cond $AE::NameProperty $text))
    if (-not $item) { throw "В списке нет '$text'" }
    SelectItem $item
    try { $ec.Collapse() } catch {}
    Start-Sleep -Milliseconds 400
}


function Enabled($el) { $el.Current.IsEnabled }
function Rows($grid) { $grid.FindAll($TS::Children, (Cond $AE::ControlTypeProperty $CT::DataItem)) }
function Nav([string]$title) {
    SelectItem (ByName (El $main 'NavList') $title)
    Start-Sleep -Milliseconds 900
    $t = TextOf $main 'PageTitle'
    Write-Host "     открыт раздел: $t"
    return $t
}
function FindDialog([string]$title, [int]$seconds = 8) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        $d = FindWindow $title
        if (-not $d) { $d = $main.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.AndCondition((Cond $AE::ControlTypeProperty $CT::Window), (Cond $AE::NameProperty $title)))) }
        if ($d) { return $d }
        Start-Sleep -Milliseconds 300
    }
    throw "Окно подтверждения '$title' не появилось"
}
function Toasts() {
    $list = @()
    foreach ($t in $main.FindAll($TS::Descendants, (Cond $AE::ControlTypeProperty $CT::Text))) {
        $n = $t.Current.Name
        if ($n -match '(сохранён|сохранены|создан|удалён|отмечен|Перемещено|Принято|опубликован|Тема|Курсы|не оплачен|Сначала|Недостаточно)') { $list += $n.Replace($nb, ' ') }
    }
    return ($list -join ' | ')
}

try { Set-DisplayResolution -Width 1366 -Height 768 -Force -ErrorAction Stop } catch { Write-Host "Разрешение не изменено: $($_.Exception.Message)" }
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host "Экран: $($b.Width)x$($b.Height)"

$exe = (Resolve-Path 'AmfibiaStudio\bin\Release\net10.0-windows\AmfibiaStudio.exe').Path
$script:proc = Start-Process -FilePath $exe -PassThru
try {
    # ===================== ВХОД =====================
    $main = WaitWindow 'AMFIBIA Studio — панель управления магазином'
    Start-Sleep -Seconds 2
    Shot '01-login'
    Check 'Вход: кнопка недоступна с пустыми полями' (-not (Enabled (El $main 'LoginButton')))
    SetValue (El $main 'LoginEmail') 'owner'
    SetValue (El $main 'LoginPassword') 'x'
    Check 'Вход: неверный email — кнопка недоступна' (-not (Enabled (El $main 'LoginButton')))
    Shot '02-login-error'
    SetValue (El $main 'LoginEmail') 'owner@amfibia.by'
    Check 'Вход: кнопка стала доступна' (Enabled (El $main 'LoginButton'))
    Press (El $main 'LoginButton')
    Check 'Вход: неверный пароль' ((TextOf $main 'LoginError') -eq 'Неверный email или пароль.') (TextOf $main 'LoginError')
    SetValue (El $main 'LoginEmail') 'buyer@amfibia.by'
    SetValue (El $main 'LoginPassword') 'Amfibia2026'
    Press (El $main 'LoginButton')
    Check 'Вход: покупателю панель закрыта' ((TextOf $main 'LoginError').Contains('только сотрудникам')) (TextOf $main 'LoginError')
    Press (ByName (El $main 'DemoAccounts') 'Создатель магазина')
    Press (El $main 'LoginButton')

    # ===================== ДАШБОРД =====================
    Check 'Дашборд открыт' ((TextOf $main 'PageTitle') -eq 'Дашборд') (TextOf $main 'PageTitle')
    Check 'Сотрудник в меню' ((TextOf $main 'UserInitials') -eq 'АВ' -and (TextOf $main 'UserRole') -eq 'Создатель магазина') (TextOf $main 'UserName')
    $navCount = (Items (El $main 'NavList') $CT::ListItem).Count
    Check 'Меню владельца: 11 разделов' ($navCount -eq 11) "$navCount"
    Start-Sleep -Seconds 1
    Shot '03-dashboard'
    SelectItem (ByName (El $main 'PeriodList') 'Год')
    Shot '04-dashboard-year'

    # ===================== ТОВАРЫ =====================
    Check 'Раздел «Товары»' ((Nav 'Товары') -eq 'Товары')
    Check 'Товары: 714' ((TextOf $main 'ProductsCount') -eq 'Показано 714 из 714') (TextOf $main 'ProductsCount')
    SetValue (El $main 'ProductsSearch') 'Triple S'
    Start-Sleep -Milliseconds 900
    Check 'Поиск товаров' ((TextOf $main 'ProductsCount') -match '^Показано [1-9]\d* из 714$') (TextOf $main 'ProductsCount')
    SelectItem (Rows (El $main 'ProductsGrid'))[0]
    $id = TextOf $main 'EditorHeader'
    Check 'Карточка товара открыта' ($id -match '^[A-Z]{2,4}-[A-Z]{3}-\d{4}$') $id
    Check 'Сохранить недоступна без изменений' (-not (Enabled (El $main 'SaveProduct')))
    SetValue (El $main 'EditorPrice') 'abc'
    Check 'Цена с ошибкой — сохранить нельзя' (-not (Enabled (El $main 'SaveProduct')))
    Shot '05-product-error'
    SetValue (El $main 'EditorPrice') '4100'
    Check 'Верная цена — сохранить можно' (Enabled (El $main 'SaveProduct'))
    Press (El $main 'SaveProduct')
    Check 'Товар сохранён' (-not (Enabled (El $main 'SaveProduct'))) (Toasts)
    Shot '06-product-saved'
    Press (El $main 'NewProduct')
    Check 'Новый товар' ((TextOf $main 'EditorHeader') -eq 'Новый товар')
    SetValue (El $main 'EditorName') 'Тестовое худи Studio'
    SetValue (El $main 'EditorPrice') '1200'
    SetValue (El $main 'EditorDescription') 'Плотный хлопок, свободный крой, вышивка на груди.'
    Check 'Новый товар можно сохранить' (Enabled (El $main 'SaveProduct'))
    Press (El $main 'SaveProduct')
    Check 'Товар создан' ((TextOf $main 'ProductsCount').EndsWith('из 715')) (TextOf $main 'ProductsCount')
    Shot '07-product-created'
    Press (El $main 'DeleteProduct')
    $dlg = FindDialog 'Удалить товар'
    Shot '08-confirm-delete'
    Press (El $dlg 'DialogConfirm')
    Start-Sleep -Milliseconds 800
    Check 'Товар удалён после подтверждения' ((TextOf $main 'ProductsCount').EndsWith('из 714')) (TextOf $main 'ProductsCount')

    # ===================== ЗАКАЗЫ =====================
    Check 'Раздел «Заказы»' ((Nav 'Заказы') -eq 'Заказы')
    Shot '09-orders'
    SelectItem (ByName (El $main 'OrderTabs') 'Новые')
    Check 'Вкладка «Новые»: 6 заказов' ((TextOf $main 'OrdersCount') -eq '6 заказов') (TextOf $main 'OrdersCount')
    $found = $false
    foreach ($r in (Rows (El $main 'OrdersGrid'))) {
        SelectItem $r
        if ((TextOf $main 'OrderStatus') -eq 'Оформлен') { $found = $true; break }
    }
    Check 'Найден заказ «Оформлен»' $found (TextOf $main 'OrderNumber')
    Press (El $main 'AdvanceOrder')
    Check 'Неоплаченный заказ картой не подтверждается' ((TextOf $main 'OrderStatus') -eq 'Оформлен') (Toasts)
    Press (El $main 'MarkPaid')
    Check 'Оплата отмечена' ((TextOf $main 'OrderPayment') -match '· оплачен$') (TextOf $main 'OrderPayment')
    Press (El $main 'AdvanceOrder')
    Check 'Заказ подтверждён' ((TextOf $main 'OrderStatus') -eq 'Подтверждён') (TextOf $main 'OrderStatus')
    Shot '10-order-confirmed'
    Press (El $main 'CancelOrder')
    $dlg = FindDialog 'Отмена заказа'
    Press (El $dlg 'DialogCancel')
    Start-Sleep -Milliseconds 600
    Check 'Отказ в диалоге — заказ не отменён' ((TextOf $main 'OrderStatus') -eq 'Подтверждён') (TextOf $main 'OrderStatus')
    Press (El $main 'CancelOrder')
    $dlg = FindDialog 'Отмена заказа'
    Press (El $dlg 'DialogConfirm')
    Start-Sleep -Milliseconds 800
    Check 'Заказ отменён — во вкладке «Новые» стало 5' ((TextOf $main 'OrdersCount') -eq '5 заказов') (TextOf $main 'OrdersCount')

    # ===================== СКЛАД =====================
    Check 'Раздел «Склад»' ((Nav 'Склад') -eq 'Склад')
    $minsk = [int](TextOf $main 'StockMinsk')
    SetValue (El $main 'ReceiveQty') '5'
    Press (El $main 'ReceiveButton')
    Check 'Приход 5 шт. в Минск' ([int](TextOf $main 'StockMinsk') -eq $minsk + 5) "$minsk -> $(TextOf $main 'StockMinsk')"
    SetValue (El $main 'TransferQty') '999'
    Check 'Перемещение больше остатка недоступно' (-not (Enabled (El $main 'TransferButton')))
    SetValue (El $main 'TransferQty') '2'
    Check 'Перемещение 2 шт. доступно' (Enabled (El $main 'TransferButton'))
    Press (El $main 'TransferButton')
    Check 'Перемещено в Москву' ([int](TextOf $main 'StockMinsk') -eq $minsk + 3) (TextOf $main 'StockMoscow')
    Shot '11-warehouse'

    # ===================== ОТЗЫВЫ =====================
    Check 'Раздел «Отзывы»' ((Nav 'Отзывы') -eq 'Отзывы')
    Check 'На модерации 9' ((TextOf $main 'ReviewsCount') -eq '9 отзывов') (TextOf $main 'ReviewsCount')
    Shot '12-reviews'
    Press (El $main 'ApproveReview')
    Check 'Отзыв опубликован' ((TextOf $main 'ReviewsCount') -eq '8 отзывов') (TextOf $main 'ReviewsCount')

    # ===================== ПРОМОКОДЫ =====================
    Check 'Раздел «Промокоды»' ((Nav 'Промокоды') -eq 'Промокоды')
    Check '3 промокода' ((Rows (El $main 'PromosGrid')).Count -eq 3)
    Press (El $main 'NewPromo')
    SetValue (El $main 'PromoCode') 'gothic15'
    Check 'Повтор промокода — сохранить нельзя' (-not (Enabled (El $main 'SavePromo')))
    SetValue (El $main 'PromoCode') 'TEST30'
    SetValue (El $main 'PromoPercent') '30'
    Check 'Промокод можно сохранить' (Enabled (El $main 'SavePromo'))
    Press (El $main 'SavePromo')
    Check 'Промокод добавлен' ((Rows (El $main 'PromosGrid')).Count -eq 4)
    Shot '13-promos'

    # ===================== КУРСЫ =====================
    Check 'Раздел «Курсы валют»' ((Nav 'Курсы валют') -eq 'Курсы валют')
    SetValue (El $main 'UsdRate') 'abc'
    Check 'Курс с ошибкой — сохранить нельзя' (-not (Enabled (El $main 'SaveRates')))
    SetValue (El $main 'UsdRate') '3,2051'
    Check 'Верный курс' (Enabled (El $main 'SaveRates'))
    Press (El $main 'LoadNbrb')
    $loaded = Wait { $s = TextOf $main 'RatesStatus'; $s.StartsWith('Официальные') -or $s.StartsWith('Не удалось') } 25
    Check 'Загрузка курсов НБРБ завершилась' $loaded (TextOf $main 'RatesStatus')
    Shot '14-rates'

    # ===================== ПОЛЬЗОВАТЕЛИ =====================
    Check 'Раздел «Пользователи»' ((Nav 'Пользователи') -eq 'Пользователи')
    ChooseInCombo (El $main 'UsersRole') 'Курьер'
    Check 'Фильтр по роли: 2 курьера' ((Rows (El $main 'UsersGrid')).Count -eq 2)
    SelectItem (Rows (El $main 'UsersGrid'))[0]
    Check 'Карточка пользователя' ((TextOf $main 'UserCardName').Length -gt 3) (TextOf $main 'UserCardName')
    Shot '15-users'

    # ===================== ЭНЦИКЛОПЕДИЯ =====================
    Check 'Раздел «Энциклопедия»' ((Nav 'Энциклопедия') -eq 'Энциклопедия модных домов')
    Check 'Статья Chrome Hearts' ((TextOf $main 'BrandName') -eq 'Chrome Hearts') (TextOf $main 'FoundText')
    Shot '16-brands'
    SelectItem (ByName (El $main 'BrandTabs') 'Хронология')
    Shot '17-brands-timeline'
    SelectItem (ByName (El $main 'BrandList') 'Balenciaga')
    Check 'Статья Balenciaga' ((TextOf $main 'BrandName') -eq 'Balenciaga')
    SelectItem (ByName (El $main 'BrandTabs') 'История')
    SetValue (El $main 'ArticleSearch') 'Баленсиага'
    Start-Sleep -Milliseconds 800
    Check 'Поиск по статье' ((TextOf $main 'FoundText').StartsWith('Найдено:')) (TextOf $main 'FoundText')
    Shot '18-brands-search'

    # ===================== ЖУРНАЛ =====================
    Check 'Раздел «Журнал»' ((Nav 'Журнал') -eq 'Журнал действий')
    SetValue (El $main 'AuditSearch') 'TEST30'
    Start-Sleep -Milliseconds 800
    Check 'Действие записано в журнал' ((Rows (El $main 'AuditGrid')).Count -ge 1)
    SetValue (El $main 'AuditSearch') ''
    Start-Sleep -Milliseconds 600
    Shot '19-audit'

    # ===================== НАСТРОЙКИ И ТЕМЫ =====================
    Check 'Раздел «Настройки»' ((Nav 'Настройки') -eq 'Настройки')
    SelectItem (ByName (El $main 'ThemeList') 'Светлая')
    Start-Sleep -Milliseconds 800
    Shot '20-theme-light'
    Nav 'Дашборд' | Out-Null
    Shot '21-dashboard-light'
    Nav 'Настройки' | Out-Null
    SelectItem (ByName (El $main 'ThemeList') 'Графит')
    Nav 'Заказы' | Out-Null
    Shot '22-orders-graphite'
    Nav 'Настройки' | Out-Null
    SelectItem (ByName (El $main 'ThemeList') 'AMFIBIA')

    # ===================== ДРУГАЯ РОЛЬ =====================
    Press (El $main 'LogoutButton')
    Check 'Выход на экран входа' ((El $main 'LoginButton') -ne $null)
    Press (ByName (El $main 'DemoAccounts') 'Курьер')
    Press (El $main 'LoginButton')
    $navCount = (Items (El $main 'NavList') $CT::ListItem).Count
    Check 'Курьер: 3 раздела, начинает с заказов' ($navCount -eq 3 -and (TextOf $main 'PageTitle') -eq 'Заказы') "$navCount"
    Shot '23-courier'

    Check 'Программа работает без сбоев' (-not $script:proc.HasExited)
}
catch {
    Write-Host "FAIL Сценарий прерван: $($_.Exception.Message)"
    $script:fails++
    try { Shot 'error' } catch {}
}
finally {
    if (-not $script:proc.HasExited) {
        $script:proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 2
        if (-not $script:proc.HasExited) { $script:proc.Kill() }
    }
}

# Второй запуск: изменения сохранились между запусками
$script:proc = Start-Process -FilePath $exe -PassThru
try {
    $main = WaitWindow 'AMFIBIA Studio — панель управления магазином'
    Start-Sleep -Seconds 2
    Press (ByName (El $main 'DemoAccounts') 'Создатель магазина')
    Press (El $main 'LoginButton')
    Nav 'Промокоды' | Out-Null
    Check 'Второй запуск: промокод TEST30 сохранён' ((Rows (El $main 'PromosGrid')).Count -eq 4)
    Shot '24-second-run'
}
catch {
    Write-Host "FAIL Второй запуск: $($_.Exception.Message)"
    $script:fails++
    try { Shot 'error2' } catch {}
}
finally {
    if (-not $script:proc.HasExited) {
        $script:proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 2
        if (-not $script:proc.HasExited) { $script:proc.Kill() }
    }
}

$crash = Join-Path $env:APPDATA 'AMFIBIA\Lab_MVVM\crash.log'
if (Test-Path $crash) { Write-Host '----- crash.log -----'; Get-Content $crash -Encoding UTF8 | ForEach-Object { Write-Host $_ }; $script:fails++ }
if ($script:fails -gt 0) { Write-Host "ПРОВАЛЕНО: $($script:fails)"; exit 1 }
Write-Host 'ВСЕ ПРОВЕРКИ ОКОН ПРОЙДЕНЫ'
