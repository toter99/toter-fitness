# toter-fitness

Version 1.0.0

Fitness-System für QBCore. Admins setzen Trainingsstationen mit Props in die Welt. Spieler trainieren dort mit einem Minispiel ihre Werte Kraft, Ausdauer, Flexibilität und Kondition (0 bis 100). Der Charakter macht die Übung sichtbar für alle mit. Kostenlose Stationen geben bis +3, Studio-Stationen kosten $50 und geben bis +6.

## Abhängigkeiten

- qb-core (oder Anbindung nach ANSCHLUESSE.md)
- oxmysql
- lb-phone
- optional: ox_target oder qb-target, qb-banking oder qb-management

## Installation

Im Ordner `resources/[toter]` des Servers:

```
git clone -b release --depth 1 https://github.com/toter99/toter-fitness.git toter-fitness
```

In der `server.cfg`:

```
ensure toter-fitness
```

Diese SQL-Dateien in der angegebenen Reihenfolge in die Datenbank einspielen:

- `sql/tabellen.sql`

## Update

```
cd resources/[toter]/toter-fitness
git pull
```

Danach neue SQL-Dateien einspielen und die Ressource neu starten.

## Mehr

Beschreibung, Einstellungen und Anbindung an andere Server: https://github.com/toter99/toter-fitness

## Lizenz

Siehe `LICENSE`. Erstellt von toter.
