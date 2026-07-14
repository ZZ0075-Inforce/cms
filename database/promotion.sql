USE [CMS]
GO
/****** Object:  Table [dbo].[FeaturedPromoItem]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FeaturedPromoItem](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[ScheduleOn] [date] NOT NULL,
	[TrainingCenter_pkid] [smallint] NOT NULL,
	[Slot] [tinyint] NOT NULL,
	[Promotion_pkid] [int] NOT NULL,
	[Topic] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](300) NOT NULL,
 CONSTRAINT [PK_FeaturedPromoItem] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_FeaturedPromoItem_UniqueDateLocSlot] UNIQUE NONCLUSTERED 
(
	[ScheduleOn] ASC,
	[TrainingCenter_pkid] ASC,
	[Slot] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Partner]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Partner](
	[pkid] [smallint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[AppKey] [varchar](10) NOT NULL,
	[NameOnPartnerMenu] [nvarchar](200) NOT NULL,
	[NameOnCourseDetailPage] [nvarchar](50) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[ImageFilename] [varchar](50) NULL,
 CONSTRAINT [PK_Partner] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PartnerCourseGroup]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PartnerCourseGroup](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Partner_pkid] [smallint] NOT NULL,
	[CourseGroup_pkid] [smallint] NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[Description] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_PartnerCourseGroup] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Promotion2]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Promotion2](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[PublishStatus_pkid] [tinyint] NOT NULL,
	[ObjectId] [uniqueidentifier] NOT NULL,
	[PromoCode] [nvarchar](30) NOT NULL,
	[Topic] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](300) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[ScheduleOn] [date] NOT NULL,
	[ScheduleOff] [date] NOT NULL,
	[EdmTopic] [nvarchar](100) NULL,
	[Image_200x100] [nvarchar](50) NULL,
	[Image_266x160] [nvarchar](50) NULL,
	[Flash_200x100] [nvarchar](30) NULL,
	[Flash_266x160] [nvarchar](30) NULL,
	[RelatedPartner_pkid] [smallint] NULL,
	[RelatedPartnerCourseGroup_pkid] [int] NULL,
	[Seminar_pkid] [int] NULL,
 CONSTRAINT [PK_Promotion2] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_Promotion2_UniquePromoCode] UNIQUE NONCLUSTERED 
(
	[PromoCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PublishStatus]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PublishStatus](
	[pkid] [tinyint] NOT NULL,
	[Description] [nvarchar](50) NOT NULL,
	[IsDraft] [bit] NOT NULL,
	[IsPublished] [bit] NOT NULL,
	[IsDiscontinued] [bit] NOT NULL,
 CONSTRAINT [PK_PublishingStatus] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Seminar]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Seminar](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[Partner_pkid] [smallint] NULL,
	[Description] [nvarchar](3000) NOT NULL,
	[attr_id] [nvarchar](50) NULL,
 CONSTRAINT [PK_Seminar] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TrainingCenter]    Script Date: 3/12/2026 3:48:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TrainingCenter](
	[pkid] [smallint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](10) NOT NULL,
	[AppKey] [varchar](3) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[City] [nvarchar](30) NOT NULL,
	[Address] [nvarchar](200) NOT NULL,
	[PhoneNumber] [nvarchar](30) NOT NULL,
	[FaxNumber] [nvarchar](30) NOT NULL,
	[Transportation] [nvarchar](2000) NOT NULL,
	[MapImageFilename] [nvarchar](50) NOT NULL,
	[CourseRegistrationEmail] [nvarchar](100) NOT NULL,
	[SeminarRegistrationEmail] [nvarchar](100) NOT NULL,
	[ServiceEmail] [nvarchar](100) NOT NULL,
	[IsDefault] [bit] NOT NULL,
	[TestCenterAddress] [nvarchar](200) NOT NULL,
	[TestCenterPhone] [nvarchar](30) NOT NULL,
	[TestCenterFax] [nvarchar](30) NOT NULL,
	[PrometricCode] [varchar](20) NOT NULL,
	[VueCode] [varchar](20) NOT NULL,
	[CorporateSalesPhone] [nvarchar](30) NOT NULL,
	[SkillTrainingFax] [nvarchar](30) NOT NULL,
	[MarketingPhone] [nvarchar](30) NOT NULL,
	[SalesEmail] [nvarchar](100) NOT NULL,
	[RetainEmailAddress] [varchar](100) NOT NULL,
	[CustomerServiceExt] [varchar](30) NOT NULL,
 CONSTRAINT [PK_TrainingCenter] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[FeaturedPromoItem]  WITH CHECK ADD  CONSTRAINT [FK_FeaturedPromoItem_Promotion21] FOREIGN KEY([Promotion_pkid])
REFERENCES [dbo].[Promotion2] ([pkid])
GO
ALTER TABLE [dbo].[FeaturedPromoItem] CHECK CONSTRAINT [FK_FeaturedPromoItem_Promotion21]
GO
ALTER TABLE [dbo].[FeaturedPromoItem]  WITH CHECK ADD  CONSTRAINT [FK_FeaturedPromoItem_TrainingCenter] FOREIGN KEY([TrainingCenter_pkid])
REFERENCES [dbo].[TrainingCenter] ([pkid])
GO
ALTER TABLE [dbo].[FeaturedPromoItem] CHECK CONSTRAINT [FK_FeaturedPromoItem_TrainingCenter]
GO
ALTER TABLE [dbo].[PartnerCourseGroup]  WITH CHECK ADD  CONSTRAINT [FK_PartnerCourseGroup_CourseGroup] FOREIGN KEY([CourseGroup_pkid])
REFERENCES [dbo].[CourseGroup] ([pkid])
GO
ALTER TABLE [dbo].[PartnerCourseGroup] CHECK CONSTRAINT [FK_PartnerCourseGroup_CourseGroup]
GO
ALTER TABLE [dbo].[PartnerCourseGroup]  WITH CHECK ADD  CONSTRAINT [FK_PartnerCourseGroup_Partner] FOREIGN KEY([Partner_pkid])
REFERENCES [dbo].[Partner] ([pkid])
GO
ALTER TABLE [dbo].[PartnerCourseGroup] CHECK CONSTRAINT [FK_PartnerCourseGroup_Partner]
GO
ALTER TABLE [dbo].[Promotion2]  WITH CHECK ADD  CONSTRAINT [FK_Promotion2_Partner] FOREIGN KEY([RelatedPartner_pkid])
REFERENCES [dbo].[Partner] ([pkid])
GO
ALTER TABLE [dbo].[Promotion2] CHECK CONSTRAINT [FK_Promotion2_Partner]
GO
ALTER TABLE [dbo].[Promotion2]  WITH CHECK ADD  CONSTRAINT [FK_Promotion2_PartnerCourseGroup] FOREIGN KEY([RelatedPartnerCourseGroup_pkid])
REFERENCES [dbo].[PartnerCourseGroup] ([pkid])
GO
ALTER TABLE [dbo].[Promotion2] CHECK CONSTRAINT [FK_Promotion2_PartnerCourseGroup]
GO
ALTER TABLE [dbo].[Promotion2]  WITH CHECK ADD  CONSTRAINT [FK_Promotion2_PublishStatus] FOREIGN KEY([PublishStatus_pkid])
REFERENCES [dbo].[PublishStatus] ([pkid])
GO
ALTER TABLE [dbo].[Promotion2] CHECK CONSTRAINT [FK_Promotion2_PublishStatus]
GO
ALTER TABLE [dbo].[Promotion2]  WITH CHECK ADD  CONSTRAINT [FK_Promotion2_Seminar] FOREIGN KEY([Seminar_pkid])
REFERENCES [dbo].[Seminar] ([pkid])
GO
ALTER TABLE [dbo].[Promotion2] CHECK CONSTRAINT [FK_Promotion2_Seminar]
GO
