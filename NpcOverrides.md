# Einzelne NPCs überschreiben

Im NPC-Gruppenmenü öffnet `NpcOverwrites` die Rollen der ausgewählten Quellaufnahme. Die Gruppen-ID steht oben; `Back` führt zur Gruppenübersicht. Avatar-ID, Stehhöhe und Sitzhöhe lassen sich je Quell-Rollen-ID ändern. Nur Unterschiede zur Originalaufnahme werden in `NpcGroupSettings.AvatarOverrides` gespeichert. `ResetToOriginButton` entfernt alle drei Überschreibungen dieser Rolle. Ein Wechsel der Quellaufnahme verwirft die bisherigen Überschreibungen dieser Gruppe, damit wiederverwendete Rollen-IDs nicht unbeabsichtigt die Einstellungen einer anderen Aufnahme bekommen.

Das Menü bearbeitet die aktuelle Preset-Konfiguration. Der bestehende Preset-Speichern-Ablauf speichert sie mit. `PresetController` kopiert die Werte in beide Richtungen zwischen Preset und `PreRecordedSceneImport`; der Import wendet sie vor dem Laden der Avatare an. Explizite NPC-Höhen haben je Feld Vorrang vor der allgemeinen Spielerhöhen-Übernahme `npcHeightOverwrites`. Nicht überschriebene Felder folgen weiterhin der bisherigen Höhenregel.

Neue Aufnahmen enthalten die effektiven Avatar- und Größenwerte in `Roles`, einschließlich `HeightOfSeatedRoleCm`. Zusätzlich hält `SessionModel.NpcGroups` eine Kopie der Quellgruppen und expliziten Überschreibungen fest. Sitzhöhen werden beim Laden wiederhergestellt. Alte Sessions ohne Sitzhöhe verwenden die bisherige Schätzung `Stehhöhe / 1.35`; das Einzel-NPC-Menü kennzeichnet dies. Die Originaldateien werden nicht geändert.

## Bewegungsumrechnung folgt separat

Dieser Schritt verändert keine Bewegungsdaten und ersetzt keine auf das alte Avatar-Rig bezogenen Aufnahme-Kalibrierungsdaten. Bei geänderter Körpergröße oder anderem Avatar können aufgezeichnete Bewegungen deshalb noch falsche Kopf-, Hand- oder Fußpositionen ergeben. Die bestehende Trennung von Aufnahme- und Live-Kalibrierung bleibt erhalten.

Ein passender nächster Integrationspunkt ist `PreRecordedTakeImporter.ImportTake`: Dort sind Quellrolle, Zielrolle, Quellkalibrierung und die bestehende Koordinatenumrechnung verfügbar. Die Anpassung sollte beim Übertragen in die neue Aufnahme einmal erfolgen, mit expliziter Kennzeichnung der Zielmaße bzw. Zielkalibrierung pro Take. Dadurch bleiben Originalaufnahmen unverändert, erneut importierte Takes werden nicht doppelt skaliert und der spätere interaktive Rollenwechsel kann weiterhin die Live-Kalibrierung verwenden. Avatarwechsel benötigen zusätzlich zur Größenanpassung eine Prüfung der Bone- und Zieloffsets.

Das Dropdown `Overwrite_SelectNpcAvatar_Dropdown` ist über seine Komponentenreferenz angebunden. Das Einzel-NPC-Panel besitzt eine eigene AvatarCatalog-Referenz; für ältere Szeneninstanzen wird ein eindeutiger Katalog derselben Szene verwendet. Die Liste enthält alle Katalog-IDs und bewahrt zusätzlich eine gegebenenfalls unbekannte Original-ID. Die Dropdown-Vorlage einschließlich Toggle und Item-Label ist im Prefab hinterlegt.
