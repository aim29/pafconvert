CREATE TABLE [paf].[localities](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[post_town] [varchar](30) NULL,
	[dependent_locality] [varchar](35) NULL,
	[double_dependent_locality] [varchar](35) NULL
) ON [PRIMARY]
GO
ALTER TABLE [paf].[localities] ADD  CONSTRAINT [PK__localiti__3213E83FB809293B] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [idx_localities] ON [paf].[localities]
(
	[post_town] ASC,
	[dependent_locality] ASC,
	[double_dependent_locality] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

CREATE TABLE [paf].[thoroughfares](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[thoroughfare] [varchar](80) NOT NULL
) ON [PRIMARY]
GO
ALTER TABLE [paf].[thoroughfares] ADD  CONSTRAINT [PK__thorough__3213E83F1558901D] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE UNIQUE NONCLUSTERED INDEX [idx_thoroughfares] ON [paf].[thoroughfares]
(
	[thoroughfare] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

CREATE TABLE [paf].[addresses](
	[id] [int] NOT NULL,
	[postcode] [varchar](8) NOT NULL,
	[locality_id] [int] NOT NULL,
	[thoroughfare_id] [int] NULL,
	[dependent_thoroughfare_id] [int] NULL,
	[building_number] [int] NULL,
	[building_name] [varchar](50) NULL,
	[sub_building_name] [varchar](30) NULL,
	[po_box] [varchar](6) NULL,
	[department_name] [varchar](60) NULL,
	[organisation_name] [varchar](60) NULL,
	[postcode_joined]  AS (CONVERT([varchar](8),replace([postcode],' ','')))
) ON [PRIMARY]
GO
ALTER TABLE [paf].[addresses] ADD PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [idx_addresses_locality_dependent_thoroughfare] ON [paf].[addresses]
(
	[locality_id] ASC,
	[dependent_thoroughfare_id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [idx_addresses_locality_thoroughfare] ON [paf].[addresses]
(
	[locality_id] ASC,
	[thoroughfare_id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [idx_addresses_postcode] ON [paf].[addresses]
(
	[postcode] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
GO
CREATE NONCLUSTERED INDEX [idx_addresses_postcode_joined] ON [paf].[addresses]
(
	[postcode_joined] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

CREATE VIEW [paf].[vw_addresses]
AS
SELECT        p.postcode, p.postcode_joined, p.locality_id, p.thoroughfare_id, p.dependent_thoroughfare_id, l.post_town, l.dependent_locality, l.double_dependent_locality, t.thoroughfare, dt.thoroughfare AS dependent_thoroughfare, 
                         p.building_number, p.building_name, p.sub_building_name, p.po_box, p.department_name, p.organisation_name, p.id AS udprn, p.id
FROM            paf.addresses AS p LEFT OUTER JOIN
                         paf.localities AS l ON p.locality_id = l.id LEFT OUTER JOIN
                         paf.thoroughfares AS t ON p.thoroughfare_id = t.id LEFT OUTER JOIN
                         paf.thoroughfares AS dt ON p.dependent_thoroughfare_id = dt.id

GO

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
GO

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
GO
