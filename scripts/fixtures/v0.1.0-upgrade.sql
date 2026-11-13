-- Synthetic upgrade fixture from the v0.1.0 release, commit 7b939d83bdbf3b9bcc7ab26327edeb738f2eb4f2.
-- Its migration history, demo accounts, and ticket #1 audit events were created by the released app.
-- The demo account password hashes are for smoke tests only.
BEGIN TRANSACTION;
CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);
CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);
INSERT INTO "AspNetRoles" VALUES('22847f63-985d-4591-b9cb-df89f6a29691','Technician','TECHNICIAN','7d3ce194-e907-4c2f-9f41-31e9c004174e');
INSERT INTO "AspNetRoles" VALUES('dec86281-36d2-4f92-8f3b-48c17f721365','Viewer','VIEWER','3af2d58a-b807-4e34-ad37-5e25233963af');
CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
INSERT INTO "AspNetUserRoles" VALUES('f9b4ba27-8caa-4b36-b764-8a720e613b7b','22847f63-985d-4591-b9cb-df89f6a29691');
INSERT INTO "AspNetUserRoles" VALUES('e64848fe-3e5f-4790-ad09-a65156cd09bf','dec86281-36d2-4f92-8f3b-48c17f721365');
CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);
CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);
INSERT INTO "AspNetUsers" VALUES('f9b4ba27-8caa-4b36-b764-8a720e613b7b','demo-tech','DEMO-TECH',NULL,NULL,0,'AQAAAAIAAYagAAAAECOp4U7ZNDqfjSRqn2r7wC31+VmYNvfFjUwqJXiN2qbPsgKNBraB9CwCM2pvQV2dTg==','NAO5BOE7J6RURIPFALMCWDIKY7Z6DDKS','cee378a1-7c36-4a12-8c41-62a1caf88c04',NULL,0,0,NULL,1,0);
INSERT INTO "AspNetUsers" VALUES('e64848fe-3e5f-4790-ad09-a65156cd09bf','demo-viewer','DEMO-VIEWER',NULL,NULL,0,'AQAAAAIAAYagAAAAEMSYuvOoUhtDPNNgxyBDyQV2+/2muy+QsUQObOz/X1DC72jwkAe6vVdC1MZOJQyUfg==','VYRJZAKWVBSPETZGHXNZPE5UYM5LYYRO','01bb3af7-a0b2-4d2b-9dc3-32b6a1332eda',NULL,0,0,NULL,1,0);
CREATE TABLE "Assets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Assets" PRIMARY KEY AUTOINCREMENT,
    "AssetTag" TEXT COLLATE NOCASE NOT NULL,
    "Hostname" TEXT NOT NULL,
    "BitLockerState" TEXT NOT NULL,
    "TpmState" TEXT NOT NULL,
    "LastCheckedAtUtc" TEXT NOT NULL
);
INSERT INTO "Assets" VALUES(1,'WS-001','FIN-WS-01','suspended','ready','2026-11-01 13:45:00');
INSERT INTO "Assets" VALUES(2,'WS-002','FIN-WS-02','protected','not-ready','2026-11-01 13:30:00');
INSERT INTO "Assets" VALUES(3,'WS-003','OPS-WS-01','protected','ready','2026-11-01 13:15:00');
INSERT INTO "Assets" VALUES(4,'WS-004','OPS-WS-02','unknown','unknown','2026-11-01 13:00:00');
INSERT INTO "Assets" VALUES(5,'WS-005','HR-WS-01','protected','ready','2026-11-01 12:45:00');
INSERT INTO "Assets" VALUES(6,'WS-006','HR-WS-02','protected','ready','2026-11-01 12:30:00');
INSERT INTO "Assets" VALUES(7,'WS-007','ENG-WS-01','off','ready','2026-11-01 12:15:00');
INSERT INTO "Assets" VALUES(8,'WS-008','ENG-WS-02','protected','ready','2026-11-01 12:00:00');
INSERT INTO "Assets" VALUES(9,'WS-009','SALES-WS-01','protected','ready','2026-11-01 11:45:00');
INSERT INTO "Assets" VALUES(10,'WS-010','DESIGN-WS-01','protected','ready','2026-11-01 11:30:00');
INSERT INTO "Assets" VALUES(11,'WS-011','SUPPORT-WS-01','protected','ready','2026-11-01 11:15:00');
INSERT INTO "Assets" VALUES(12,'WS-012','TRAINING-WS-01','unknown','missing','2026-11-01 11:00:00');
CREATE TABLE "TicketEvents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TicketEvents" PRIMARY KEY AUTOINCREMENT,
    "TicketId" INTEGER NOT NULL,
    "PreviousStatus" TEXT NOT NULL,
    "NewStatus" TEXT NOT NULL,
    "Technician" TEXT NOT NULL,
    "Note" TEXT NULL,
    "OccurredAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_TicketEvents_Tickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "Tickets" ("Id") ON DELETE RESTRICT
);
INSERT INTO "TicketEvents" VALUES(1,2,'open','in-progress','Alex Morgan',NULL,'2026-11-01 03:00:00');
INSERT INTO "TicketEvents" VALUES(2,5,'open','in-progress','Alex Morgan',NULL,'2026-10-31 09:00:00');
INSERT INTO "TicketEvents" VALUES(3,5,'in-progress','resolved','Alex Morgan','Verified the reported issue and recorded the outcome.','2026-10-31 11:00:00');
INSERT INTO "TicketEvents" VALUES(4,6,'open','in-progress','Alex Morgan',NULL,'2026-10-31 03:00:00');
INSERT INTO "TicketEvents" VALUES(5,8,'open','in-progress','Alex Morgan',NULL,'2026-10-30 15:00:00');
INSERT INTO "TicketEvents" VALUES(6,8,'in-progress','resolved','Alex Morgan','Verified the reported issue and recorded the outcome.','2026-10-30 16:00:00');
INSERT INTO "TicketEvents" VALUES(7,10,'open','in-progress','Alex Morgan',NULL,'2026-10-30 03:00:00');
INSERT INTO "TicketEvents" VALUES(8,11,'open','in-progress','Alex Morgan',NULL,'2026-10-29 21:00:00');
INSERT INTO "TicketEvents" VALUES(9,11,'in-progress','resolved','Alex Morgan','Verified the reported issue and recorded the outcome.','2026-10-30 01:00:00');
INSERT INTO "TicketEvents" VALUES(10,13,'open','in-progress','Alex Morgan',NULL,'2026-10-29 09:00:00');
INSERT INTO "TicketEvents" VALUES(11,14,'open','in-progress','Alex Morgan',NULL,'2026-10-29 03:00:00');
INSERT INTO "TicketEvents" VALUES(12,14,'in-progress','resolved','Alex Morgan','Verified the reported issue and recorded the outcome.','2026-10-29 06:00:00');
INSERT INTO "TicketEvents" VALUES(13,16,'open','in-progress','Alex Morgan',NULL,'2026-10-28 15:00:00');
INSERT INTO "TicketEvents" VALUES(14,17,'open','in-progress','Alex Morgan',NULL,'2026-10-28 09:00:00');
INSERT INTO "TicketEvents" VALUES(15,17,'in-progress','resolved','Alex Morgan','Verified the reported issue and recorded the outcome.','2026-10-28 11:00:00');
INSERT INTO "TicketEvents" VALUES(16,18,'open','in-progress','Alex Morgan',NULL,'2026-10-28 03:00:00');
INSERT INTO "TicketEvents" VALUES(17,1,'open','in-progress','demo-tech',NULL,'2026-11-12 22:01:00.406896');
INSERT INTO "TicketEvents" VALUES(18,1,'in-progress','resolved','demo-tech','Preserve this audit event from v0.1.0.','2026-11-12 22:01:00.428253');
CREATE TABLE "Tickets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tickets" PRIMARY KEY AUTOINCREMENT,
    "AssetId" INTEGER NOT NULL,
    "Summary" TEXT NOT NULL,
    "Priority" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "AssignedTechnician" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ResolvedAtUtc" TEXT NULL,
    CONSTRAINT "FK_Tickets_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES "Assets" ("Id") ON DELETE RESTRICT
);
INSERT INTO "Tickets" VALUES(1,1,'BitLocker protection suspended after restart','P2','resolved','demo-tech','2026-11-01 08:00:00','2026-11-12 22:01:00.428253');
INSERT INTO "Tickets" VALUES(2,2,'TPM is not ready after firmware update','P2','in-progress','Alex Morgan','2026-11-01 02:00:00',NULL);
INSERT INTO "Tickets" VALUES(3,3,'Workstation cannot connect to the office network','P1','open',NULL,'2026-10-31 20:00:00',NULL);
INSERT INTO "Tickets" VALUES(4,4,'External monitor flickers after sign-in','P3','open',NULL,'2026-10-31 14:00:00',NULL);
INSERT INTO "Tickets" VALUES(5,5,'Requested headset replacement','P4','resolved','Alex Morgan','2026-10-31 08:00:00','2026-10-31 11:00:00');
INSERT INTO "Tickets" VALUES(6,6,'Shared printer missing after restart','P3','in-progress','Alex Morgan','2026-10-31 02:00:00',NULL);
INSERT INTO "Tickets" VALUES(7,7,'BitLocker check reports protection off','P2','open',NULL,'2026-10-30 20:00:00',NULL);
INSERT INTO "Tickets" VALUES(8,8,'USB dock drops network connection','P2','resolved','Alex Morgan','2026-10-30 14:00:00','2026-10-30 16:00:00');
INSERT INTO "Tickets" VALUES(9,9,'New starter needs a workstation','P3','open',NULL,'2026-10-30 08:00:00',NULL);
INSERT INTO "Tickets" VALUES(10,10,'Disk space alert on design workstation','P2','in-progress','Alex Morgan','2026-10-30 02:00:00',NULL);
INSERT INTO "Tickets" VALUES(11,11,'Video calls have no audio input','P3','resolved','Alex Morgan','2026-10-29 20:00:00','2026-10-30 01:00:00');
INSERT INTO "Tickets" VALUES(12,12,'Screen timeout setting requested','P4','open',NULL,'2026-10-29 14:00:00',NULL);
INSERT INTO "Tickets" VALUES(13,3,'Keyboard disconnects during sign-in','P3','in-progress','Alex Morgan','2026-10-29 08:00:00',NULL);
INSERT INTO "Tickets" VALUES(14,4,'Encryption check has not reported','P2','resolved','Alex Morgan','2026-10-29 02:00:00','2026-10-29 06:00:00');
INSERT INTO "Tickets" VALUES(15,5,'Update pending restart','P4','open',NULL,'2026-10-28 20:00:00',NULL);
INSERT INTO "Tickets" VALUES(16,6,'Workstation is stuck at the sign-in screen','P1','in-progress','Alex Morgan','2026-10-28 14:00:00',NULL);
INSERT INTO "Tickets" VALUES(17,7,'Install approved PDF reader','P4','resolved','Alex Morgan','2026-10-28 08:00:00','2026-10-28 11:00:00');
INSERT INTO "Tickets" VALUES(18,8,'Confirm replacement mouse delivery','P4','in-progress','Alex Morgan','2026-10-28 02:00:00',NULL);
INSERT INTO "Tickets" VALUES(19,9,'VPN connection drops on Wi-Fi','P2','open',NULL,'2026-10-27 20:00:00',NULL);
INSERT INTO "Tickets" VALUES(20,10,'Request for a second monitor','P4','open',NULL,'2026-10-27 14:00:00',NULL);
CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);
INSERT INTO "__EFMigrationsHistory" VALUES('20261112213013_InitialCreate','10.0.14');
INSERT INTO "__EFMigrationsHistory" VALUES('20261112213315_AddIdentity','10.0.14');
CREATE TABLE "__EFMigrationsLock" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
    "Timestamp" TEXT NOT NULL
);
CREATE UNIQUE INDEX "IX_Assets_AssetTag" ON "Assets" ("AssetTag");
CREATE INDEX "IX_TicketEvents_TicketId" ON "TicketEvents" ("TicketId");
CREATE INDEX "IX_Tickets_AssetId" ON "Tickets" ("AssetId");
CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
DELETE FROM "sqlite_sequence";
INSERT INTO "sqlite_sequence" VALUES('Assets',12);
INSERT INTO "sqlite_sequence" VALUES('Tickets',20);
INSERT INTO "sqlite_sequence" VALUES('TicketEvents',18);
COMMIT;
