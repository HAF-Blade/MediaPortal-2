using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Utilities;

namespace MediaPortal.Extensions.MetadataExtractors
{
    /// <summary>
    /// MediaPortal 2 metadata extractor for NFO series files
    /// </summary>

    public class NfoSeriesMetadataExtractor : IMetadataExtractor
    {

        #region Helper classes for simple XML deserialization

        [XmlRoot(ElementName = "actor")]
        public class Actor
        {
            [XmlElement(ElementName = "name")]
            public string name { get; set; }

            [XmlElement(ElementName = "role")]
            public string role { get; set; }

            [XmlElement(ElementName = "thumb")]
            public string thumb { get; set; }
        }

        [XmlRoot(ElementName = "fanart")]
        public class Fanart
        {
            [XmlElement(ElementName = "thumb")]
            public string pic { get; set; }
        }

        [XmlRoot(ElementName = "tvshow")]
        public class TvShow
        {
            [XmlElement(ElementName = "title")]
            public string title { get; set; }

            [XmlElement(ElementName = "showtitle")]
            public string showtitle { get; set; }

            [XmlElement(ElementName = "rating")]
            public double rating { get; set; }

            [XmlElement(ElementName = "year")]
            public int year { get; set; }

            [XmlElement(ElementName = "top250")]
            public int top250 { get; set; }

            [XmlElement(ElementName = "season")]
            public int season { get; set; }

            [XmlElement(ElementName = "episode")]
            public int episode { get; set; }

            [XmlElement(ElementName = "displayseason")]
            public int displaySeason { get; set; }

            [XmlElement(ElementName = "displayepisode")]
            public int displayEpisode { get; set; }

            [XmlElement(ElementName = "votes")]
            public int votes { get; set; }

            [XmlElement(ElementName = "outline")]
            public string outline { get; set; }

            [XmlElement(ElementName = "plot")]
            public string plot { get; set; }

            [XmlElement(ElementName = "tagline")]
            public string tagline { get; set; }

            [XmlElement(ElementName = "runtime")]
            public string runtime { get; set; }

            [XmlElement(ElementName = "thumb")]
            public string[] thumb { get; set; }

            [XmlElement(ElementName = "fanart")]
            public Fanart fanart { get; set; }

            [XmlElement(ElementName = "mpaa")]
            public string mpaa { get; set; }

            [XmlElement(ElementName = "playcount")]
            public int playCount { get; set; }

            [XmlElement(ElementName = "lastplayed")]
            public string lastPlayed { get; set; }

            [XmlElement(ElementName = "id")]
            public string id { get; set; }

            [XmlElement(ElementName = "genre")]
            public string[] genre { get; set; }

            [XmlElement(ElementName = "set")]
            public string set { get; set; }

            [XmlElement(ElementName = "credits")]
            public string credits { get; set; }

            [XmlElement(ElementName = "director")]
            public string director { get; set; }

            [XmlElement(ElementName = "premiered")]
            public string premiered { get; set; }

            [XmlElement(ElementName = "status")]
            public string status { get; set; }

            [XmlElement(ElementName = "code")]
            public string code { get; set; }

            [XmlElement(ElementName = "aired")]
            public string aired { get; set; }

            [XmlElement(ElementName = "studio")]
            public string studio { get; set; }

            [XmlElement(ElementName = "trailer")]
            public string trailer { get; set; }

            [XmlElement(ElementName = "actor")]
            public Actor[] actor { get; set; }
        }

        [XmlRoot(ElementName = "episodedetails")]
        public class EpisodeDetails
        {
            [XmlElement(ElementName = "title")]
            public string title { get; set; }

            [XmlElement(ElementName = "showtitle")]
            public string showtitle { get; set; }

            [XmlElement(ElementName = "rating")]
            public double rating { get; set; }

            [XmlElement(ElementName = "season")]
            public int season { get; set; }

            [XmlElement(ElementName = "episode")]
            public int episode { get; set; }

            [XmlElement(ElementName = "plot")]
            public string plot { get; set; }

            [XmlElement(ElementName = "thumb")]
            public string[] thumb { get; set; }

            [XmlElement(ElementName = "playcount")]
            public int playCount { get; set; }

            [XmlElement(ElementName = "lastplayed")]
            public string lastPlayed { get; set; }

            [XmlElement(ElementName = "credits")]
            public string credits { get; set; }

            [XmlElement(ElementName = "director")]
            public string director { get; set; }

            [XmlElement(ElementName = "aired")]
            public string aired { get; set; }

            [XmlElement(ElementName = "premiered")]
            public string premiered { get; set; }

            [XmlElement(ElementName = "studio")]
            public string studio { get; set; }

            [XmlElement(ElementName = "mpaa")]
            public string mpaa { get; set; }

            [XmlElement(ElementName = "epbookmark")]
            public int epbookmark { get; set; }

            [XmlElement(ElementName = "displayseason")]
            public int displaySeason { get; set; }

            [XmlElement(ElementName = "displayepisode")]
            public int displayEpisode { get; set; }

            [XmlElement(ElementName = "actor")]
            public Actor[] actor { get; set; }

        }

        #endregion

        #region Constants
        /// <summary>
        /// GUID string for the NFO metadata extractor.
        /// </summary>
        public const string METADATAEXTRACTOR_ID_STR = "EC0684E1-C9D0-4F53-AED7-E7FDF567DE9C";

        /// <summary>
        /// Tve3 metadata extractor GUID.
        /// </summary>
        public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);


        public const string MEDIA_CATEGORY_NAME_SERIES = "Series";

        #endregion

        #region Class variables
        protected MetadataExtractorMetadata _metadata; //Metadata which has been found
        protected static IList<MediaCategory> MEDIA_CATEGORIES = new List<MediaCategory>();
        protected static XmlSerializer _xmlSerializerShow = null; // Lazy initialized
        protected static XmlSerializer _xmlSerializerEpisode = null; // Lazy initialized
        #endregion

        #region Constructors
        static NfoSeriesMetadataExtractor()
        {
            MEDIA_CATEGORIES.Add(DefaultMediaCategories.Video);

            IMediaAccessor mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();

            //Series
            MediaCategory seriesCategory;
            if (!mediaAccessor.MediaCategories.TryGetValue(MEDIA_CATEGORY_NAME_SERIES, out seriesCategory))
            {
                seriesCategory = mediaAccessor.RegisterMediaCategory(MEDIA_CATEGORY_NAME_SERIES, new List<MediaCategory> { DefaultMediaCategories.Video });
            }
            MEDIA_CATEGORIES.Add(seriesCategory);
        }

        public NfoSeriesMetadataExtractor()
        {
          ServiceRegistration.Get<ILogger>().Info("Creating MetadataExtractorMetadata instance");
          _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "NFO series metadata extractor", MetadataExtractorPriority.External, false,
              MEDIA_CATEGORIES, new[]
                  {
                    MediaAspect.Metadata,
                    VideoAspect.Metadata,
                    SeriesAspect.Metadata
                  });
          ServiceRegistration.Get<ILogger>().Info("MetadataExtractorMetadata instance created");
        }

        #endregion


        #region IMetadataExtractor implementation

        public MetadataExtractorMetadata Metadata
        {
            get { return _metadata; }
        }

        public bool TryExtractMetadata(MediaPortal.Common.ResourceAccess.IResourceAccessor mediaItemAccessor, IDictionary<Guid, MediaItemAspect> extractedAspectData, bool forceQuickMode)
        {

            ServiceRegistration.Get<ILogger>().Info("NFO series metadata extractor running");

            try
            {

                /*
                 * Process file path
                 */
                IFileSystemResourceAccessor fsra = mediaItemAccessor as IFileSystemResourceAccessor;
                if (fsra == null || !mediaItemAccessor.IsFile)
                {
                    return false;
                }

                string filePath = mediaItemAccessor.CanonicalLocalResourcePath.ToString();
                string ext = ProviderPathHelper.GetExtension(filePath);
                string fileWithoutExt = ProviderPathHelper.GetFileNameWithoutExtension(filePath);
                string dirName = ProviderPathHelper.GetDirectoryName(filePath);
                string nfoSeriesPath1 = dirName + "/tvshow.nfo";
                string nfoEpisodePath1 = ProviderPathHelper.ChangeExtension(filePath, ".nfo");

                if (ext != ".ts" && ext != ".mkv" && ext != ".avi" && ext != ".mp4" && ext != ".mpeg" && ext != ".mpg" && ext != ".flv")
                {
                    return false;
                }

                bool ok = false;

                ServiceRegistration.Get<ILogger>().Info("NFO series metadata extractor running for {0}", filePath);

                //Series data extraction
                ok = TryExtractSeries(extractedAspectData, forceQuickMode, nfoSeriesPath1, nfoEpisodePath1);

                //TODO FileInfo
                //TODO VIDEO_TS/<videofile>.nfo
                //TODO <folder>.nfo
                //TODO VIDEO_TS/<folder>.nfo
                //TODO VIDEO_TS/movie.nfo

                return ok;

            }
            catch (Exception e)
            {
                ServiceRegistration.Get<ILogger>().Info("NfoMetadataExtractor: Exception reading resource '{0}' (Text: '{1}')", mediaItemAccessor.CanonicalLocalResourcePath, e.Message);
            }
            return false;
        }

        protected XmlSerializer GetTvShowXmlSerializer()
        {
            return _xmlSerializerShow ?? (_xmlSerializerShow = new XmlSerializer(typeof(TvShow)));
        }

        protected XmlSerializer GetEpisodeDetailsXmlSerializer()
        {
            return _xmlSerializerEpisode ?? (_xmlSerializerEpisode = new XmlSerializer(typeof(EpisodeDetails)));
        }

        private bool TryExtractSeries(IDictionary<Guid, MediaItemAspect> extractedAspectData, bool forceQuickMode, string seriesNfo, string episodeNfo)
        {


            string path = seriesNfo + " / " + episodeNfo;

            try
            {
                /*
                 * Deserialization of xml data
                 */
                IResourceAccessor metaFileAccessor1 = null;
                IResourceAccessor metaFileAccessor2 = null;

                bool seriesNfoOk = false;
                bool episodeNfoOk = false;

                if (ResourcePath.Deserialize(seriesNfo).TryCreateLocalResourceAccessor(out metaFileAccessor1))
                    seriesNfoOk = true;
                if (ResourcePath.Deserialize(episodeNfo).TryCreateLocalResourceAccessor(out metaFileAccessor2))
                    episodeNfoOk = true;                
                if (seriesNfoOk == false || episodeNfoOk == false || metaFileAccessor1 == null || metaFileAccessor2 == null)
                    return false;


                TvShow tagsTvShow = null;
                using (metaFileAccessor1)
                {
                    using (Stream metaStream1 = metaFileAccessor1.OpenRead())
                    {
                        tagsTvShow = (TvShow)GetTvShowXmlSerializer().Deserialize(metaStream1);
                    }
                }

                EpisodeDetails tagsEpDet = null;
                using (metaFileAccessor2)
                {
                    using (Stream metaStream2 = metaFileAccessor2.OpenRead())
                    {
                        tagsEpDet = (EpisodeDetails)GetEpisodeDetailsXmlSerializer().Deserialize(metaStream2);
                    }
                }

                if (tagsTvShow == null || tagsEpDet == null)
                    return false;

                /*
                 * Initialize aspects
                 */
                MediaItemAspect mediaAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, MediaAspect.Metadata);
                MediaItemAspect videoAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, VideoAspect.Metadata);
                MediaItemAspect seriesAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, SeriesAspect.Metadata);

                /*
                 * Series handling
                 */
                ServiceRegistration.Get<ILogger>().Info("Processing NFO data for series {0} {1}", tagsTvShow, tagsEpDet);

                seriesAspect.SetAttribute(SeriesAspect.ATTR_SERIESNAME, tagsTvShow.title);
                seriesAspect.SetAttribute(SeriesAspect.ATTR_SEASON, tagsEpDet.season);

                List<int> episodes = new List<int>();
                episodes.Add(tagsEpDet.episode);
                seriesAspect.SetCollectionAttribute(SeriesAspect.ATTR_EPISODE, episodes);

                seriesAspect.SetAttribute(SeriesAspect.ATTR_EPISODENAME, tagsEpDet.title);

                List<String> genres = new List<String>();
                foreach (string genre in tagsTvShow.genre)
                {
                    string[] split = genre.Split(new Char[] { ' ', ',', '.', ':' });
                    foreach (string s in split)
                    {
                        if (s.Trim() != "")
                            genres.Add(s);
                    }
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_GENRES, genres);

                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_DURATION, );
                videoAspect.SetAttribute(VideoAspect.ATTR_DIRECTOR, tagsEpDet.director);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOSTREAMCOUNT);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOENCODING);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOBITRATE);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOLANGUAGES);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_VIDEOENCODING);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_VIDEOBITRATE);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_WIDTH);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_HEIGHT);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ASPECTRATIO);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_FPS);

                List<String> actors = new List<String>();
                foreach (Actor a in tagsEpDet.actor)
                {
                    actors.Add(a.name);
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ACTORS, actors);

                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ISDVD,);
                videoAspect.SetAttribute(VideoAspect.ATTR_STORYPLOT, tagsEpDet.plot);

                mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, tagsEpDet.title);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE,);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_RECORDINGTIME, );
                //mediaAspect.SetAttribute(MediaAspect.ATTR_RATING, );
                //mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT, );
                mediaAspect.SetAttribute(MediaAspect.ATTR_PLAYCOUNT, tagsEpDet.playCount);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_LASTPLAYED, tagsEpDet.lastPlayed);

                return true;
            }
            catch (Exception e)
            {
                ServiceRegistration.Get<ILogger>().Info("NfoMetadataExtractor: Exception reading resource '{0}' (Text: '{1}')", path, e.Message);
            }

            return false;

        }

        #endregion

    }

}


