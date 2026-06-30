# BA-ReTool

BA-ReTool - открытая часть инструмента для анализа локальных файлов ресурсов Blue Archive PC/Steam.

## Что умеет программа

- Вычислять пароль ZIP-архива TableBundles по имени архива.
- Экспортировать `ExcelDB.db` из SQLCipher в обычную SQLite-базу.
- Проверять, что экспортированная база читается как обычная SQLite.

## Требования

- Windows x64.
- Установленный клиент Blue Archive PC/Steam.
- Для разработки нужен установленный .NET SDK, совместимый с `net10.0`.
- Для опубликованного автономного релиза отдельно установленная среда выполнения .NET не требуется.

## Сборка

Откройте PowerShell в папке проекта и выполните:

```powershell
dotnet build .\BA-ReTool.csproj
```

## Использование

Откройте PowerShell в папке с собранной программой.

### Вычислить пароль ZIP-архива

```powershell
.\BA-ReTool.exe zip-password --bundle Excel.zip
```

или:

```powershell
.\BA-ReTool.exe zip-password --zip "D:\Steam\steamapps\common\BlueArchive\BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\Excel.zip"
```

Вместо диска `D:` укажите свой реальный путь к папке Steam.

### Экспортировать ExcelDB.db

Обычный запуск использует встроенные SQLCipher key/license:

```powershell
.\BA-ReTool.exe export-exceldb --game-dir "D:\Steam\steamapps\common\BlueArchive"
```

Путь вывода по умолчанию:

```text
.\out\ExcelDB_plain.db
```

Можно указать свой путь:

```powershell
.\BA-ReTool.exe export-exceldb --game-dir "D:\Steam\steamapps\common\BlueArchive" --output ".\out\ExcelDB_plain.db"
```

Если после обновления игры ключи изменятся, можно временно передать новые значения вручную:

```powershell
.\BA-ReTool.exe export-exceldb --game-dir "D:\Steam\steamapps\common\BlueArchive" --key-hex "<KEY_HEX>" --license "<LICENSE>"
```

### Проверить экспортированную базу

```powershell
.\BA-ReTool.exe verify-sqlite --db ".\out\ExcelDB_plain.db"
```

Ожидаемый результат:

```text
Plain SQLite: True
```

## Входные файлы

BA-ReTool читает файлы из локальной установки Blue Archive:

```text
<GAME_DIR>\BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db
<GAME_DIR>\BlueArchive_Data\Plugins\x86_64\sqlcipher.dll
```

Для вычисления пароля ZIP-архива требуется только точное имя ZIP-файла.

## Выходные файлы

По умолчанию результаты сохраняются рядом с исполняемым файлом:

```text
out\ExcelDB_plain.db
work\ExcelDB_encrypted_workcopy.db
```

Оригинальные игровые файлы не изменяются.

## Что не входит в открытую версию

В открытую папку специально не добавлены:

- `BA-KeyCapture.csproj`;
- `KeyCaptureProgram.cs`;
- `ba_sqlcipher_hook_relay.dll`;
- `sqlcipher_hook.cpp`;
- `inject_sqlcipher_hook.ps1`;
- скрипты сканирования памяти;
- приватные отчеты reverse engineering;
- готовые закрытые бинарники KeyCapture.

Закрытый KeyCapture нужен только для поиска новых ключей после обновлений игры.
