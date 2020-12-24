create table paf.localities
(
	id int not null
		constraint PK_localities
			primary key,
	post_town varchar(30),
	dependent_locality varchar(35),
	double_dependent_locality varchar(35)
)
go

create unique index idx_localities
	on paf.localities (post_town, dependent_locality, double_dependent_locality)
go

create table paf.thoroughfares
(
	id int not null
		constraint PK_thoroughfares
			primary key,
	thoroughfare varchar(80) not null
)
go

create unique index idx_thoroughfares
	on paf.thoroughfares (thoroughfare)
go

create table paf.addresses
(
	id int not null
		constraint PK_addresses
			primary key,
	postcode varchar(8) not null,
	locality_id int not null,
	thoroughfare_id int,
	dependent_thoroughfare_id int,
	building_number int,
	building_name varchar(50),
	sub_building_name varchar(30),
	po_box varchar(6),
	department_name varchar(60),
	organisation_name varchar(60),
	postcode_joined varchar(8)
)
go

create index idx_addresses_locality_dependent_thoroughfare
	on paf.addresses (locality_id, dependent_thoroughfare_id)
go

create index idx_addresses_locality_thoroughfare
	on paf.addresses (locality_id, thoroughfare_id)
go

create index idx_addresses_postcode
	on paf.addresses (postcode)
go

create index idx_addresses_postcode_joined
	on paf.addresses (postcode_joined)
go

CREATE VIEW paf.vw_addresses
AS
SELECT        p.postcode, p.postcode_joined, p.locality_id, p.thoroughfare_id, p.dependent_thoroughfare_id, l.post_town, l.dependent_locality, l.double_dependent_locality, t.thoroughfare, dt.thoroughfare AS dependent_thoroughfare, 
                         p.building_number, p.building_name, p.sub_building_name, p.po_box, p.department_name, p.organisation_name, p.id AS udprn, p.id
FROM            paf.addresses AS p LEFT OUTER JOIN
                         paf.localities AS l ON p.locality_id = l.id LEFT OUTER JOIN
                         paf.thoroughfares AS t ON p.thoroughfare_id = t.id LEFT OUTER JOIN
                         paf.thoroughfares AS dt ON p.dependent_thoroughfare_id = dt.id
go

CREATE PROCEDURE [paf].[find_addresses]
(
    @postcode varchar(8) = '',
	@town varchar(80) = '',
	@street varchar(100) = '',
	@house varchar(100) = ''
)
AS
BEGIN
    SET NOCOUNT ON

	DECLARE	@thoroughfares NVARCHAR(MAX),
		@localities NVARCHAR(MAX),
		@statement NVARCHAR(MAX)

	SET @postcode = REPLACE(ISNULL(@postcode,''),' ','')
	SET @town = ISNULL(@town,'')
	SET @street = ISNULL(@street,'')
	SET @house = ISNULL(@house,'')

	IF (@postcode = '' AND @town = '')
	BEGIN
		RAISERROR(14624, -1, -1, 'postcode, town')
		RETURN
	END

	SET @statement = 'select top (100) * from paf.vw_addresses
		where
		postcode_joined like @postcode+''%'''
	
	PRINT 'POSTCODE: ' + @postcode

	IF (@town = '')
	BEGIN
		PRINT 'NO TOWN'
		-- No town
		IF (@street != '')
		BEGIN
			-- No town, has a street
			PRINT 'STREET: ' + @street

			SELECT @thoroughfares = STRING_AGG(id,',') from paf.thoroughfares where thoroughfare like @street+'%';
			SET @statement = @statement + '
				and (
					thoroughfare_id in (' + @thoroughfares +')
					or dependent_thoroughfare_id in (' + @thoroughfares +')
				)'
		END
	END
	ELSE
	BEGIN
		-- Has a town
		PRINT 'TOWN: ' + @town

		SELECT @localities = STRING_AGG(id,',') from paf.localities	where post_town like @town+'%' or dependent_locality like @town+'%' or double_dependent_locality like @town+'%';

		IF (@street = '')
		BEGIN
			PRINT 'NO STREET'
			-- Has a town, no street
			SET @statement = @statement + '
				and locality_id in (' + @localities +')'
		END
		ELSE
		BEGIN
			PRINT 'STREET: ' + @street
			-- Has a town, has a street

			SELECT @thoroughfares = STRING_AGG(id,',') from paf.thoroughfares where thoroughfare like @street+'%';
			SET @statement = @statement + '
				and ((
					locality_id in (' + @localities +')
					and	thoroughfare_id in (' + @thoroughfares +')
				) or (
					locality_id in (' + @localities +')
					and dependent_thoroughfare_id in (' + @thoroughfares +')
				))'
		END
	END

	IF (@house != '')
	BEGIN
		PRINT 'HOUSE: ' + @house
		-- Has a house number or name
		SET @statement = @statement + '
			and (
				building_name like ''%''+@house+''%''
				or sub_building_name like ''%''+@house+''%''
				or building_number like ''%''+@house+''%''
			)'
	END

	SET @statement = @statement + ';'

	PRINT @statement

	EXEC sp_executesql @statement, N'@postcode VARCHAR(8) OUTPUT, @house VARCHAR(80) OUTPUT',
		@postcode OUTPUT, @house OUTPUT
END
go

CREATE PROCEDURE [paf].[find_postcodes]
(
    @postcode varchar(8) = ''
)
AS
BEGIN
    SET NOCOUNT ON

	SET @postcode = REPLACE(ISNULL(@postcode,''),' ','')

	IF (@postcode = '')
	BEGIN
		RAISERROR(14624, -1, -1, 'postcode')
		RETURN
	END

	select distinct top (100) postcode from paf.addresses
		where postcode_joined like @postcode+'%'
		order by postcode;
END
go