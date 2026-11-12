-- toter-fitness: alle Tabellen in ihrer aktuellen Form.
-- Nur zum Nachschauen. Der Server legt die Tabellen beim Start selbst an und ergänzt fehlende Spalten.
-- Erzeugt aus den CREATE- und ALTER-Befehlen im Server-Code (MariaDB).

CREATE TABLE IF NOT EXISTS `fitness_gyms` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(60) NOT NULL,
  `created_at` int(10) unsigned NOT NULL DEFAULT 0,
  `job` varchar(50) NOT NULL DEFAULT '',
  `preis_tag` int(10) unsigned NOT NULL DEFAULT 100,
  `preis_woche` int(10) unsigned NOT NULL DEFAULT 500,
  `preis_monat` int(10) unsigned NOT NULL DEFAULT 1500,
  `empfang` varchar(80) DEFAULT NULL,
  `shop` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `fitness_kurse` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `gym_id` int(11) NOT NULL,
  `titel` varchar(60) NOT NULL,
  `ort` varchar(80) NOT NULL DEFAULT '',
  `x` float NOT NULL,
  `y` float NOT NULL,
  `z` float NOT NULL,
  `start` bigint(20) NOT NULL,
  `dauer` int(11) NOT NULL,
  `leiter` varchar(80) NOT NULL DEFAULT '',
  `leiter_cid` varchar(60) NOT NULL DEFAULT '',
  `teilnehmer` text DEFAULT NULL,
  `uebung` varchar(40) NOT NULL DEFAULT 'flexibilitaet:kurs-yoga',
  `max` int(11) NOT NULL DEFAULT 10,
  `richtung` float NOT NULL DEFAULT 0,
  `route` int(11) NOT NULL DEFAULT 0,
  `preis` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `fitness_probetage` (
  `citizenid` varchar(50) NOT NULL,
  `gym_id` int(11) NOT NULL,
  `zeit` bigint(20) NOT NULL,
  PRIMARY KEY (`citizenid`,`gym_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `fitness_rekorde` (
  `citizenid` varchar(50) NOT NULL,
  `uebung` varchar(30) NOT NULL,
  `wert` int(11) NOT NULL DEFAULT 0,
  `name` varchar(60) NOT NULL DEFAULT '',
  `datum` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`citizenid`,`uebung`),
  KEY `uebung_wert` (`uebung`,`wert`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `fitness_stations` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `stat_type` enum('kraft','ausdauer','flexibilitaet','kondition') NOT NULL,
  `station_type` enum('kostenlos','gym','outdoor') NOT NULL DEFAULT 'kostenlos',
  `position_x` float NOT NULL,
  `position_y` float NOT NULL,
  `position_z` float NOT NULL,
  `heading` float NOT NULL DEFAULT 0,
  `radius` float NOT NULL DEFAULT 2.5,
  `prop_model` varchar(100) NOT NULL DEFAULT '',
  `prop_height` float NOT NULL DEFAULT 0,
  `route` text DEFAULT NULL,
  `created_by` varchar(50) DEFAULT NULL,
  `created_at` int(10) unsigned NOT NULL DEFAULT 0,
  `gym_id` int(10) unsigned DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_stat_type` (`stat_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `fitness_verlauf` (
  `citizenid` varchar(50) NOT NULL,
  `tag` bigint(20) NOT NULL,
  `kraft` int(11) NOT NULL,
  `ausdauer` int(11) NOT NULL,
  `flexibilitaet` int(11) NOT NULL,
  `kondition` int(11) NOT NULL,
  PRIMARY KEY (`citizenid`,`tag`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `player_fitness` (
  `citizenid` varchar(50) NOT NULL,
  `kraft` int(10) unsigned NOT NULL DEFAULT 50,
  `ausdauer` int(10) unsigned NOT NULL DEFAULT 50,
  `flexibilitaet` int(10) unsigned NOT NULL DEFAULT 50,
  `kondition` int(10) unsigned NOT NULL DEFAULT 50,
  `last_training` int(10) unsigned NOT NULL DEFAULT 0,
  `last_login` int(10) unsigned NOT NULL DEFAULT 0,
  `last_abbau` int(10) unsigned NOT NULL DEFAULT 0,
  `total_training_sessions` int(10) unsigned NOT NULL DEFAULT 0,
  `buff_shake_bis` int(10) unsigned NOT NULL DEFAULT 0,
  `buff_vitamine_bis` int(10) unsigned NOT NULL DEFAULT 0,
  `muskelkater_ab` int(10) unsigned NOT NULL DEFAULT 0,
  `muskelkater_bis` int(10) unsigned NOT NULL DEFAULT 0,
  `last_kraft` int(10) unsigned NOT NULL DEFAULT 0,
  `abbau_kraft` int(10) unsigned NOT NULL DEFAULT 0,
  `last_ausdauer` int(10) unsigned NOT NULL DEFAULT 0,
  `abbau_ausdauer` int(10) unsigned NOT NULL DEFAULT 0,
  `last_flexibilitaet` int(10) unsigned NOT NULL DEFAULT 0,
  `abbau_flexibilitaet` int(10) unsigned NOT NULL DEFAULT 0,
  `last_kondition` int(10) unsigned NOT NULL DEFAULT 0,
  `abbau_kondition` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`citizenid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
