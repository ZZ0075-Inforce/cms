USE [CMS]
GO
/****** Object:  Table [dbo].[Certification]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Certification](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Partner_pkid] [smallint] NOT NULL,
	[Title] [nchar](100) NULL,
 CONSTRAINT [PK_Certification] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_Certification] UNIQUE NONCLUSTERED 
(
	[pkid] ASC,
	[Partner_pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Course]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Course](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[OfficialTitle] [nvarchar](300) NULL,
	[CourseId] [varchar](50) NOT NULL,
	[ProdCourseId] [varchar](50) NOT NULL,
	[FriendlyUrl] [nvarchar](100) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[Partner_pkid] [smallint] NOT NULL,
	[CourseGroup_pkid] [smallint] NULL,
	[PublishStatus_pkid] [tinyint] NOT NULL,
	[ScheduleOn] [date] NOT NULL,
	[ScheduleOff] [date] NOT NULL,
	[Hour] [smallint] NOT NULL,
	[ListPrice] [decimal](9, 0) NOT NULL,
	[LearningCredit] [decimal](9, 1) NOT NULL,
	[Material] [nvarchar](500) NULL,
	[Objective] [nvarchar](4000) NULL,
	[Target] [nvarchar](500) NULL,
	[Prerequisites] [nvarchar](4000) NULL,
	[Outline] [nvarchar](max) NULL,
	[TowardCertOrExam] [nvarchar](max) NULL,
	[Note] [nvarchar](4000) NULL,
	[OtherInfo] [nvarchar](4000) NULL,
	[CanRepeat] [bit] NOT NULL,
 CONSTRAINT [PK_Course] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseFAQ]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseFAQ](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Course_pkid] [int] NOT NULL,
	[Question] [nvarchar](200) NULL,
	[Answer] [nvarchar](2000) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
 CONSTRAINT [PK_CourseFAQ] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseGroup]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseGroup](
	[pkid] [smallint] IDENTITY(1,1) NOT NULL,
	[Description] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_CourseGroup] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseInCertification]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseInCertification](
	[Course_pkid] [int] NOT NULL,
	[Certification_pkid] [int] NOT NULL,
 CONSTRAINT [PK_CourseInCertification] PRIMARY KEY CLUSTERED 
(
	[Course_pkid] ASC,
	[Certification_pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseJobCategories]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseJobCategories](
	[Course_pkid] [int] NOT NULL,
	[JobCategory_pkid] [smallint] NOT NULL,
 CONSTRAINT [PK_CourseJobCategories] PRIMARY KEY CLUSTERED 
(
	[Course_pkid] ASC,
	[JobCategory_pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseRecomm]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseRecomm](
	[CourseId] [varchar](50) NOT NULL,
	[RecommCourseId] [varchar](50) NOT NULL,
	[CourseOrder] [int] NOT NULL,
 CONSTRAINT [PK_CourseRecomm] PRIMARY KEY CLUSTERED 
(
	[CourseId] ASC,
	[RecommCourseId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CourseRelatedLink]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CourseRelatedLink](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Course_pkid] [int] NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[LinkDefinition_pkid] [int] NOT NULL,
 CONSTRAINT [PK_CourseRelatedLink] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[HotCourse]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[HotCourse](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
	[Course_pkid] [int] NOT NULL,
	[Enable] [bit] NOT NULL,
 CONSTRAINT [PK_HotCourse_1] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[JobCategory]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[JobCategory](
	[pkid] [smallint] IDENTITY(1,1) NOT NULL,
	[Description] [nvarchar](70) NOT NULL,
 CONSTRAINT [PK_JobCategory] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LinkDefinition]    Script Date: 2/24/2026 1:46:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LinkDefinition](
	[pkid] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[LinkURL] [nvarchar](500) NOT NULL,
	[Text] [nvarchar](100) NOT NULL,
	[Tooltip] [nvarchar](500) NULL,
	[Target] [varchar](20) NULL,
	[ClassName] [varchar](50) NULL,
	[Alt] [nvarchar](50) NULL,
 CONSTRAINT [PK_LinkDefinition] PRIMARY KEY CLUSTERED 
(
	[pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Partner]    Script Date: 2/24/2026 1:46:31 PM ******/
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
/****** Object:  Table [dbo].[PartnerCourseGroup]    Script Date: 2/24/2026 1:46:31 PM ******/
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
/****** Object:  Table [dbo].[PublishStatus]    Script Date: 2/24/2026 1:46:31 PM ******/
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
/****** Object:  Table [dbo].[TrainingCenter]    Script Date: 2/24/2026 1:46:31 PM ******/
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

/****** Object:  Table [dbo].[CertificationJobCategories]    Script Date: 2/25/2026 1:41:35 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CertificationJobCategories](
	[Certification_pkid] [int] NOT NULL,
	[JobCategory_pkid] [smallint] NOT NULL,
 CONSTRAINT [PK_CertificationJobCategories] PRIMARY KEY CLUSTERED 
(
	[Certification_pkid] ASC,
	[JobCategory_pkid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[CertificationJobCategories]  WITH CHECK ADD  CONSTRAINT [FK_CertificationJobCategories_Certification] FOREIGN KEY([Certification_pkid])
REFERENCES [dbo].[Certification] ([pkid])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CertificationJobCategories] CHECK CONSTRAINT [FK_CertificationJobCategories_Certification]
GO
ALTER TABLE [dbo].[CertificationJobCategories]  WITH CHECK ADD  CONSTRAINT [FK_CertificationJobCategories_JobCategory] FOREIGN KEY([JobCategory_pkid])
REFERENCES [dbo].[JobCategory] ([pkid])
GO
ALTER TABLE [dbo].[CertificationJobCategories] CHECK CONSTRAINT [FK_CertificationJobCategories_JobCategory]





ALTER TABLE [dbo].[Course] ADD  CONSTRAINT [DF_Course_Hour]  DEFAULT ((0)) FOR [Hour]
GO
ALTER TABLE [dbo].[Course] ADD  CONSTRAINT [DF_Course_ListPrice]  DEFAULT ((0)) FOR [ListPrice]
GO
ALTER TABLE [dbo].[Course] ADD  CONSTRAINT [DF_Course_LearningCredits]  DEFAULT ((0)) FOR [LearningCredit]
GO
ALTER TABLE [dbo].[Course] ADD  CONSTRAINT [DF_Course_CanRepeat]  DEFAULT ((0)) FOR [CanRepeat]
GO
ALTER TABLE [dbo].[Course] ADD [OtherInfo] [nvarchar](4000) NULL
GO
ALTER TABLE [dbo].[HotCourse] ADD  CONSTRAINT [DF_HotCourse_Enable]  DEFAULT ((1)) FOR [Enable]
GO
ALTER TABLE [dbo].[Certification]  WITH CHECK ADD  CONSTRAINT [FK_Certification_Partner] FOREIGN KEY([Partner_pkid])
REFERENCES [dbo].[Partner] ([pkid])
GO
ALTER TABLE [dbo].[Certification] CHECK CONSTRAINT [FK_Certification_Partner]
GO
ALTER TABLE [dbo].[Course]  WITH CHECK ADD  CONSTRAINT [FK_Course_CourseGroup] FOREIGN KEY([CourseGroup_pkid])
REFERENCES [dbo].[CourseGroup] ([pkid])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Course] CHECK CONSTRAINT [FK_Course_CourseGroup]
GO
ALTER TABLE [dbo].[Course]  WITH CHECK ADD  CONSTRAINT [FK_Course_Partner] FOREIGN KEY([Partner_pkid])
REFERENCES [dbo].[Partner] ([pkid])
GO
ALTER TABLE [dbo].[Course] CHECK CONSTRAINT [FK_Course_Partner]
GO
ALTER TABLE [dbo].[Course]  WITH CHECK ADD  CONSTRAINT [FK_Course_PublishStatus] FOREIGN KEY([PublishStatus_pkid])
REFERENCES [dbo].[PublishStatus] ([pkid])
GO
ALTER TABLE [dbo].[Course] CHECK CONSTRAINT [FK_Course_PublishStatus]
GO
ALTER TABLE [dbo].[CourseFAQ]  WITH CHECK ADD  CONSTRAINT [FK_CourseFAQ_Course] FOREIGN KEY([Course_pkid])
REFERENCES [dbo].[Course] ([pkid])
GO
ALTER TABLE [dbo].[CourseFAQ] CHECK CONSTRAINT [FK_CourseFAQ_Course]
GO
ALTER TABLE [dbo].[CourseInCertification]  WITH CHECK ADD  CONSTRAINT [FK_CourseInCertification_Certification] FOREIGN KEY([Certification_pkid])
REFERENCES [dbo].[Certification] ([pkid])
GO
ALTER TABLE [dbo].[CourseInCertification] CHECK CONSTRAINT [FK_CourseInCertification_Certification]
GO
ALTER TABLE [dbo].[CourseInCertification]  WITH CHECK ADD  CONSTRAINT [FK_CourseInCertification_Course] FOREIGN KEY([Course_pkid])
REFERENCES [dbo].[Course] ([pkid])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CourseInCertification] CHECK CONSTRAINT [FK_CourseInCertification_Course]
GO
ALTER TABLE [dbo].[CourseJobCategories]  WITH CHECK ADD  CONSTRAINT [FK_CourseJobCategories_Course] FOREIGN KEY([Course_pkid])
REFERENCES [dbo].[Course] ([pkid])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CourseJobCategories] CHECK CONSTRAINT [FK_CourseJobCategories_Course]
GO
ALTER TABLE [dbo].[CourseJobCategories]  WITH CHECK ADD  CONSTRAINT [FK_CourseJobCategories_JobCategory] FOREIGN KEY([JobCategory_pkid])
REFERENCES [dbo].[JobCategory] ([pkid])
GO
ALTER TABLE [dbo].[CourseJobCategories] CHECK CONSTRAINT [FK_CourseJobCategories_JobCategory]
GO
ALTER TABLE [dbo].[CourseRelatedLink]  WITH CHECK ADD  CONSTRAINT [FK_CourseRelatedLink_Course] FOREIGN KEY([Course_pkid])
REFERENCES [dbo].[Course] ([pkid])
GO
ALTER TABLE [dbo].[CourseRelatedLink] CHECK CONSTRAINT [FK_CourseRelatedLink_Course]
GO
ALTER TABLE [dbo].[CourseRelatedLink]  WITH CHECK ADD  CONSTRAINT [FK_CourseRelatedLink_LinkDefinition] FOREIGN KEY([LinkDefinition_pkid])
REFERENCES [dbo].[LinkDefinition] ([pkid])
GO
ALTER TABLE [dbo].[CourseRelatedLink] CHECK CONSTRAINT [FK_CourseRelatedLink_LinkDefinition]
GO
ALTER TABLE [dbo].[HotCourse]  WITH CHECK ADD  CONSTRAINT [FK_HotCourse_Course] FOREIGN KEY([Course_pkid])
REFERENCES [dbo].[Course] ([pkid])
GO
ALTER TABLE [dbo].[HotCourse] CHECK CONSTRAINT [FK_HotCourse_Course]
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
