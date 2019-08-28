CREATE TABLE "addresses" (
	"id"	int NOT NULL,
	"postcode"	varchar(8) NOT NULL,
	"locality_id"	int NOT NULL,
	"thoroughfare_id"	int,
	"dependent_thoroughfare_id"	int,
	"building_number"	int,
	"building_name"	varchar(50),
	"sub_building_name"	varchar(30),
	"po_box"	varchar(6),
	"department_name"	varchar(60),
	"organisation_name"	varchar(60),
	PRIMARY KEY("id")
);
CREATE TABLE "thoroughfares" (
	"id"	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	"thoroughfare"	varchar(80) NOT NULL
);
CREATE TABLE "localities" (
	"id"	INTEGER PRIMARY KEY AUTOINCREMENT,
	"post_town"	varchar(30),
	"dependent_locality"	varchar(35),
	"double_dependent_locality"	varchar(35)
);
CREATE INDEX IF NOT EXISTS "idx_addresses_locality_dependent_thoroughfare" ON "addresses" (
	"locality_id",
	"dependent_thoroughfare_id"
);
CREATE INDEX IF NOT EXISTS "idx_addresses_locality_thoroughfare" ON "addresses" (
	"locality_id",
	"thoroughfare_id"
);
CREATE INDEX IF NOT EXISTS "idx_addresses_postcode" ON "addresses" (
	"postcode"
);
CREATE UNIQUE INDEX IF NOT EXISTS "idx_thoroughfares" ON "thoroughfares" (
	"thoroughfare"
);
CREATE UNIQUE INDEX IF NOT EXISTS "idx_localities" ON "localities" (
	"post_town",
	"dependent_locality",
	"double_dependent_locality"
);

CREATE VIEW vw_addresses (postcode,locality_id,thoroughfare_id,dependent_thoroughfare_id,post_town,dependent_locality,double_dependent_locality,thoroughfare,dependent_thoroughfare,building_number,building_name,sub_building_name,po_box,department_name,organisation_name,udprn)
as
SELECT
p.postcode,
p.locality_id,
p.thoroughfare_id,
p.dependent_thoroughfare_id,
l.post_town,
l.dependent_locality,
l.double_dependent_locality,
t.thoroughfare,
dt.thoroughfare,
p.building_number,
p.building_name,
p.sub_building_name,
p.po_box,
p.department_name,
p.organisation_name,
p.id
from addresses p
left join localities l on p.locality_id = l.id
left join thoroughfares t on p.thoroughfare_id = t.id
left join thoroughfares dt on p.dependent_thoroughfare_id = dt.id;