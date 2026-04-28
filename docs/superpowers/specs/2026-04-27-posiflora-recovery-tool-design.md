# Posiflora Recovery Tool — дизайн MVP

**Status:** approved design, awaiting plan
**Date:** 2026-04-27
**Owner:** Igor
**Project:** `D:\PROJECTS\Posiflora_monitoring`

## Контекст

Текущий проект `Posiflora_monitoring` — это одиночный Python/PyInstaller tray-монитор `Posiflora UEM Monitor`.

Он уже умеет:
- проверять службы `uem-agent` и `uem-updater` через WMI/CIM;
- проверять локальные порты `5050` и `5051`;
- проверять активное соединение UEM Agent с облаком АТОЛ по портам `443` и `1883`;
- писать лог в `C:\Posiflora\uem_monitor.log`;
- показывать статус через `tkinter`-окно и `pystray`;
- пытаться перезапускать UEM-службы при проблемах.

Локальная проверка текущей машины показала полезный реальный кейс: службы `uem-agent` и `uem-updater` есть в SCM и настроены на автозапуск, но находятся в `Stopped`, а пути `C:\Program Files\UEM\Agent\bin\uema.exe` и `C:\Program Files\UEM\Updater\bin\uemu.exe` указывают на отсутствующие бинарники. Это должен стать fixture-кейсом MVP.

Из Teamly были просмотрены релевантные статьи:
- `Решение проблем`;
- `F.A.Q. по решению проблем`;
- `Настроить кассу на работу с ОФД`;
- `Проверяем прошивку / Атол ККТ`.

Ключевой вывод из базы знаний: MVP должен быть не просто монитором UEM, а диагностом проблем кассы/АТОЛ на ПК клиента. Основные сценарии поддержки: `Атол. Нет связи`, `Необходимо запустить службу UEMA (11)`, сеть кассы, Wi-Fi, USB/COM-порты, EoU/EoT, ОФД, firewall/Defender, драйвер ККТ и прошивка 5.17.0+.

## Цель MVP

Создать Windows-native recovery tool для техподдержки Posiflora, который быстро отвечает на вопрос:

> Почему на этом ПК не работает связка POSiFLORA ↔ касса/АТОЛ/UEM, что именно сломано, что можно безопасно исправить автоматически, а что надо отдать оператору как инструкцию?

Инструмент должен:
- запускать диагностические профили по типовым сценариям;
- выдавать структурированные findings с доказательствами;
- выполнять безопасные recovery-действия;
- собирать отчет для передачи в поддержку;
- опираться на правила из Teamly, но не требовать постоянного доступа к Teamly в MVP.

## Не цель MVP

- Полноценный универсальный Windows diagnostic suite.
- Полная карта LAN-устройств и постоянный сетевой мониторинг.
- Автоматическая переустановка драйверов АТОЛ.
- Удаление `C:\ProgramData\ATOL\EoU` без явного ручного решения оператора.
- Автоматическое изменение настроек кассы, ОФД, регистрации ККТ или лицензий.
- Offline RAG по всей базе знаний.
- Удаленное управление клиентскими ПК.

## Выбранный стек

Целевой стек: **.NET 10 LTS + Windows Service Worker + WPF UI + Wpf.Ui**.

Причины:
- проект строго Windows-first;
- нужны службы, Event Log, firewall, процессы, сетевые адаптеры, COM/USB, UAC и installer;
- .NET Worker Service хорошо подходит для долгоживущего фонового процесса;
- WPF дает зрелый Windows-native UI без лишней web-обвязки;
- Wpf.Ui используется как UI-библиотека для современного Fluent/Windows-style интерфейса, navigation shell, theme resources, icons and controls;
- .NET 10 является LTS-релизом с поддержкой до ноября 2028.

Python-версия остается reference-реализацией для существующих PowerShell/CIM проверок. Основную ветку продукта на Python дальше не развиваем.

## Архитектура

Система состоит из четырех основных проектов.

### `Posiflora.Recovery.Agent`

Фоновый `.NET 10 Worker Service`.

Ответственность:
- выполнение диагностических профилей;
- выполнение recovery-действий;
- доступ к службам Windows, CIM/WMI, Event Log, firewall, TCP connections, USB/COM;
- сбор отчетов;
- audit log действий.

Agent работает с повышенными правами: администратор или LocalSystem. UI не должен сам выполнять опасные операции.

### `Posiflora.Recovery.App`

WPF-приложение для оператора техподдержки.

Ответственность:
- показать общий статус машины;
- запустить профиль диагностики;
- показать findings простым языком;
- запросить безопасное исправление;
- открыть лог или отчет;
- показать ручные шаги из базы знаний.

UI не должен напрямую вызывать `Start-Service`, менять firewall или читать privileged Event Log. Для этого используется Agent.

UI строится на Wpf.Ui и MVVM. Визуальный стиль: современный минималистичный Windows/Fluent-интерфейс с плотной рабочей компоновкой, спокойной светлой темой по умолчанию, четкими severity states и без декоративных маркетинговых блоков.

### `Posiflora.Recovery.Core`

Тестируемое ядро правил.

Ответственность:
- модели `Check`, `Finding`, `Evidence`, `RemediationAction`, `DiagnosticProfile`;
- правила интерпретации результатов;
- report builder;
- mapping низкоуровневых данных Windows в понятные выводы.

`Core` не зависит от WPF, Windows Service и конкретного способа выполнения PowerShell/CIM-команд.

### `Posiflora.Recovery.Windows`

Адаптеры к Windows.

Ответственность:
- SCM/services;
- CIM/WMI;
- Event Log;
- firewall/Defender;
- NetTCPIP;
- USB/COM devices;
- process list;
- file system probes.

Все адаптеры должны быть спрятаны за интерфейсами, чтобы Core можно было тестировать через fake data.

### IPC

UI общается с Agent через локальный IPC:
- предпочтительно named pipes;
- альтернативно gRPC over named pipes, если это упростит контракт.

Контракт IPC:
- `GetHealth()`;
- `RunProfile(profileId)`;
- `RunCheck(checkId)`;
- `RunRemediation(actionId, findingId)`;
- `GenerateReport()`;
- `TailLogs()`.

## Диагностические профили MVP

### `UEMA / ошибка 11`

Проверки:
- существует ли служба `uem-agent`;
- существует ли служба `uem-updater`;
- статус служб;
- `StartMode`;
- `ProcessId`;
- существует ли файл по `PathName`;
- есть ли активные TCP-соединения процесса UEM к облаку по `443`/`1883`;
- слушаются ли локальные порты `5050`/`5051`;
- есть ли блокирующие firewall/Defender признаки;
- установлена ли версия драйвера ККТ, если это можно определить безопасно.

Recovery:
- запустить stopped-службу;
- перезапустить running-службу;
- создать allow-rules firewall для известных UEM/ATOL бинарников;
- открыть отчет и показать ручную инструкцию по переустановке драйвера ККТ, если бинарник отсутствует или служба не установлена.

### `Нет связи с кассой по сети`

Проверки:
- активный сетевой адаптер;
- Wi-Fi SSID, BSSID, channel, signal, radio type;
- IP, gateway, DNS;
- наличие VPN/виртуальных адаптеров, которые могут запутать маршрутизацию;
- доступность IP кассы, если оператор ввел IP;
- доступность порта `5555`;
- firewall hints;
- находится ли ПК в сети, отличной от ожидаемой.

Recovery:
- безопасные проверки и подсказки;
- инструкции: одна сеть на всех устройствах, сверить IP с чека, проверить `5555`, перезапустить кассу и роутер, проверить client isolation в роутере.

### `USB / COM`

Проверки:
- COM-порты ATOL;
- устройства с ошибками в Device Manager;
- признаки занятого COM-порта;
- служба EoU;
- USB power saving hints, если доступны;
- наличие USB hub как warning, если можно определить надежно.

Recovery:
- остановить/перезапустить EoU;
- перезапустить UEM/EoU;
- инструкция: сменить кабель, сменить порт, не использовать USB hub, проверить драйвер COM-портов.

### `ОФД`

Проверки:
- интернет;
- DNS;
- доступность указанных хостов/портов ОФД, если оператор ввел параметры;
- UEM/EoU состояние;
- firewall/Defender;
- выбранный канал передачи данных, если его можно получить из локального состояния.

Recovery:
- только безопасные сетевые проверки;
- инструкции из Teamly: проверить оплату ОФД, адрес/порт/ИНН ОФД, канал связи, перезагрузить кассу после изменения настроек.

## Модель `Finding`

Каждый результат диагностики должен возвращаться как структурированный объект:

```text
id: stable machine-readable id
severity: info | warning | critical
title: короткий заголовок для UI
evidence: факты с машины
explanation: человеческое объяснение
recommendedAction: что делать оператору
canAutoFix: true | false
actions: список доступных remediation actions
source: Teamly article / built-in rule / Windows evidence
```

Примеры `id`:
- `uem.service.missing`;
- `uem.service.stopped`;
- `uem.service.missing_binary`;
- `uem.cloud.no_connection`;
- `atol.firewall.rule_missing`;
- `network.cash_register.port_5555_unreachable`;
- `wifi.signal.low`;
- `usb.atol_com_port_error`;
- `eou.service.stopped`;
- `ofd.dns_failed`.

## UI MVP

Главный экран:
- общий статус `OK / Warning / Critical`;
- время последней проверки;
- кнопки `Проверить`, `Собрать отчет`, `Открыть лог`;
- список профилей диагностики;
- список findings;
- панель выбранного finding: evidence, explanation, recommended action, кнопки safe repair.

UI должен быть рабочим инструментом техподдержки, не маркетинговым dashboard.

Требования:
- компактная плотная раскладка;
- clear severity colors;
- copyable evidence;
- кнопка копирования summary для чата с клиентом;
- ссылка на локальный отчет;
- без автоматического выполнения destructive actions.

## Локальные данные

В MVP без базы данных.

Пути:
- `C:\ProgramData\PosifloraRecovery\config.json`;
- `C:\ProgramData\PosifloraRecovery\logs\agent.log`;
- `C:\ProgramData\PosifloraRecovery\reports\`;
- `C:\ProgramData\PosifloraRecovery\cache\`.

`config.json` содержит:
- known service names;
- known ATOL/UEM paths;
- report retention;
- diagnostic profile defaults;
- known ports: `5050`, `5051`, `5555`, `443`, `1883`;
- optional known OFD endpoints.

Если позже понадобится история замеров, добавить SQLite отдельным этапом.

## Отчеты

MVP-отчет — ZIP в `C:\ProgramData\PosifloraRecovery\reports\`.

Содержимое:
- `summary.txt` — human-readable отчет;
- `results.json` — structured findings;
- `agent.log`;
- `services.json`;
- `network-adapters.json`;
- `tcp-connections.json`;
- `com-devices.json`;
- `eventlog-system.txt`;
- `eventlog-application.txt`;
- `firewall-rules.txt`;
- `environment.txt`.

Отчет не должен собирать:
- пароли;
- cookies;
- browser history;
- токены;
- личные файлы клиента;
- лишние документы из пользовательского профиля.

Потенциально чувствительные значения надо маскировать там, где они не нужны для диагностики.

## Recovery action levels

### `safe`

Read-only проверки и сбор отчетов. Выполняются без отдельного подтверждения.

### `repair`

Безопасные изменения:
- start service;
- restart service;
- create firewall allow rule;
- flush DNS cache, если будет добавлено позже.

Требуют подтверждения в UI и пишутся в audit log.

### `manual/destructive`

Только инструкция в MVP:
- удаление драйверов;
- переустановка драйвера ККТ;
- удаление `C:\ProgramData\ATOL\EoU`;
- изменение настроек кассы;
- изменение настроек ОФД;
- действия с регистрацией/перерегистрацией ККТ;
- действия с лицензиями АТОЛ.

## Логирование и audit

Agent пишет structured logs.

Каждый check логирует:
- start/end;
- duration;
- status;
- exception, если есть;
- summary result.

Каждый repair-action логирует:
- action id;
- finding id;
- timestamp;
- requested by UI;
- input parameters;
- result;
- error, если есть.

UI показывает операторскую версию логов, но полный лог остается в отчете.

## Ошибки и деградация

Если Agent недоступен:
- UI показывает `Agent unavailable`;
- предлагает запуск/переустановку службы;
- не выполняет privileged actions напрямую.

Если нет прав администратора:
- Agent должен явно вернуть finding `agent.permissions.insufficient`;
- UI показывает, какие проверки недоступны.

Если PowerShell/CIM команда зависла:
- timeout;
- partial result;
- finding с evidence о timeout;
- продолжить остальные проверки.

Если Teamly недоступен:
- MVP не зависит от Teamly runtime;
- встроенные правила и ручные инструкции хранятся локально в коде/config.

## Тестирование

Unit tests:
- Core mapping raw check outputs → findings;
- severity rules;
- report builder;
- action eligibility.

Fake Windows adapters:
- service exists/running;
- service exists/stopped;
- service missing;
- service path points to missing binary;
- TCP connection exists/missing;
- port listening/missing;
- COM device OK/error.

Integration smoke tests на Windows:
- Agent starts;
- UI connects to fake or local Agent;
- report generation works;
- restart action is blocked in test mode unless explicitly enabled.

Обязательный fixture из текущей машины:
- `uem-agent` и `uem-updater` есть в SCM;
- обе службы stopped;
- `PathName` указывает на отсутствующий exe;
- результат должен быть `critical` finding `uem.service.missing_binary` плюс recommended action по переустановке/восстановлению драйвера ККТ.

## Acceptance criteria

- UI запускается и видит Agent.
- Профиль `UEMA / ошибка 11` выдает structured findings по текущей машине.
- Missing binary для `uema.exe` и `uemu.exe` определяется как отдельная причина, а не просто “служба остановлена”.
- Recovery `start/restart service` доступен только когда это имеет смысл.
- Firewall exception action доступен только для существующих бинарников.
- Отчет ZIP создается и содержит human-readable summary и JSON findings.
- В отчет не попадают секреты браузера, cookies, пароли и пользовательские документы.
- Core tests покрывают основные состояния служб, портов, TCP-соединений и COM/USB.

## Следующий шаг

После ревью этого spec надо написать implementation plan для первой реализации:
1. scaffold `.NET 10` solution;
2. Core models and test fixtures;
3. Windows adapters for services/files/TCP;
4. Agent service skeleton;
5. WPF + Wpf.Ui UI skeleton;
6. UEMA diagnostic profile;
7. report generation;
8. packaging/install strategy.

## Источники

- Teamly: `Решение проблем`.
- Teamly: `F.A.Q. по решению проблем`.
- Teamly: `Настроить кассу на работу с ОФД`.
- Teamly: `Проверяем прошивку / Атол ККТ`.
- Microsoft Learn: .NET releases and support — https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- Microsoft Learn: Worker Services in .NET — https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
- Microsoft Learn: WPF overview — https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- NuGet: WPF-UI package — https://www.nuget.org/packages/wpf-ui/
- Microsoft Learn: Get-NetTCPConnection — https://learn.microsoft.com/en-us/powershell/module/nettcpip/get-nettcpconnection
- Microsoft Learn: Get-NetAdapter — https://learn.microsoft.com/en-us/powershell/module/netadapter/get-netadapter
- Microsoft Learn: Get-CimInstance — https://learn.microsoft.com/en-us/powershell/module/cimcmdlets/get-ciminstance
