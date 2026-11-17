# Aufnahmeabschluss

Die Schliessen-Schaltfläche verwendet weiterhin `FlowController.RequestReturnToMenu`. `StopSessionForMenu` beendet die Aufnahme, und `TickSessionSaveForMenu` wartet auch auf das verzögerte Audio-Finalisieren. Erst nach erfolgreichem Abschluss wird der Pfad der tatsächlich aufgenommenen Session festgehalten. Nach der bestehenden Rückkehr in den Konfigurationszustand wird `RecordingEndPanel` geöffnet. Reines Playback ohne gestartete Aufnahme öffnet das Panel nicht.

`WorkshopConfigurationUI.RecordingEnd` findet das vorhandene Panel, bindet `RecordingEndPanelUI` automatisch an und registriert es in `MenuNavigation`. Alle Bedienelemente werden anhand ihrer vorhandenen Namen innerhalb dieses Panels verbunden. Die Hauptmenü-Schaltfläche behält ihre vorhandene Navigation.

## Aktionen

- **RemoveLastTake_Button:** Entfernt die höchste `SceneCount` aus `SessionModel.Takes` und die zugehörigen WAV- und Frames-Dateien. A0/B0/B1 wird zu A0/B0; A0/B0/A1/B1 wird ebenfalls zu A0/B0. Gemeinsam referenzierte Dateien verbleibender Takes bleiben erhalten. Fehlende Audiodateien sind zulässig. Vor der JSON-Aktualisierung werden Dateien vorübergehend verschoben; schlägt die Aktualisierung fehl, werden sie wiederhergestellt. Die bestehende Session wird vor jeder Änderung frisch vom Datenträger gelesen.
- **DeleteLastRecording:** Löscht ausschließlich den zu diesem Panel gehörenden Session-Ordner samt Inhalt. Ein passender Verweis auf die letzte Aufnahme wird aus den PlayerPrefs entfernt.
- **Speichern unter:** Die Dropdowns zeigen vorhandene Workshop- und Unterordner des konfigurierten Aufnahme-Speichers, standardmäßig `SessionRecordingData`. Die Aufnahme wird vollständig kopiert; die ursprüngliche Aufnahme bleibt bestehen. Im neuen `session.json` wird `SessionId` auf den neuen Ordnernamen gesetzt. Take-Dateinamen und Kalibrierungsdaten bleiben erhalten. Erst nach vollständigem Kopieren wird der neue Ordner veröffentlicht; vorhandene Ziele werden niemals überschrieben. Nach dem Speichern bearbeitet das Panel die neue Kopie, die auch als letzte Aufnahme vorgemerkt wird.

Ein leerer oder ungültiger Name sowie ein bestehender Name im ausgewählten Zielordner deaktivieren den Speichern-Button. Namenskonflikte erscheinen rot unter dem Eingabefeld. Nach Löschen der Session werden alle zugehörigen Bearbeitungsaktionen deaktiviert; nach Entfernen aller Takes wird nur die Take-Entfernung deaktiviert.

Die Dateiverwaltung liegt in `RecordingSessionEditor`. Quellpfade, Dateinamen und Zielpfade werden auf gültige Komponenten geprüft; Verzeichnis- und Dateiverknüpfungen werden nicht verfolgt. Das Panel fängt Dateifehler ab und zeigt sie an.
