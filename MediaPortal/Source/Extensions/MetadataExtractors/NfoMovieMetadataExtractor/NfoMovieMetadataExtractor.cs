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
    /// MediaPortal 2 metadata extractor for NFO files
    /// </summary>

    public class NfoMovieMetadataExtractor : IMetadataExtractor
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

        [XmlRoot(ElementName = "movie")]
        public class Movie
        {
            [XmlElement(ElementName = "title")]
            public string title { get; set; }

            [XmlElement(ElementName = "originaltitle")]
            public string originaltitle { get; set; }

            [XmlElement(ElementName = "sorttitle")]
            public string sorttitle { get; set; }

            [XmlElement(ElementName = "set")]
            public string set { get; set; }

            [XmlElement(ElementName = "rating")]
            public double rating { get; set; }

            [XmlElement(ElementName = "year")]
            public string year { get; set; }

            [XmlElement(ElementName = "top250")]
            public string top250 { get; set; }

            [XmlElement(ElementName = "votes")]
            public string votes { get; set; }

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

            [XmlElement(ElementName = "mpaa")]
            public string mpaa { get; set; }

            [XmlElement(ElementName = "playcount")]
            public int playCount { get; set; }

            [XmlElement(ElementName = "watched")]
            public bool watched { get; set; }

            [XmlElement(ElementName = "id")]
            public string imdbId { get; set; }

            [XmlElement(ElementName = "filenameandpath")]
            public string fileNameAndPath { get; set; }

            [XmlElement(ElementName = "trailer")]
            public string trailer { get; set; }

            [XmlElement(ElementName = "genre")]
            public string genre { get; set; }

            [XmlElement(ElementName = "credits")]
            public string credits { get; set; }

            [XmlElement(ElementName = "director")]
            public string director { get; set; }

            [XmlElement(ElementName = "actor")]
            public Actor[] actor { get; set; }

            public override string ToString()
            {
                return "Movie " + title + " (id: " + imdbId + ")";
            }
        }

        #endregion

        #region Constants
        /// <summary>
        /// GUID string for the NFO metadata extractor.
        /// </summary>
        public const string METADATAEXTRACTOR_ID_STR = "EC0684E1-C9D0-4F53-AED7-E7FDF567DE9B";

        /// <summary>
        /// Tve3 metadata extractor GUID.
        /// </summary>
        public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);
        public const string MEDIA_CATEGORY_NAME_MOVIE = "Movie";

        #endregion

        #region Class variables
        protected MetadataExtractorMetadata _metadata; //Metadata which has been found
        protected static IList<MediaCategory> MEDIA_CATEGORIES = new List<MediaCategory>();
        protected static XmlSerializer _xmlSerializer = null; // Lazy initialized
        #endregion

        #region Constructors
        static NfoMovieMetadataExtractor()
        {
            MEDIA_CATEGORIES.Add(DefaultMediaCategories.Video);

            IMediaAccessor mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();
            
            //Movie
            MediaCategory movieCategory;
            if (!mediaAccessor.MediaCategories.TryGetValue(MEDIA_CATEGORY_NAME_MOVIE, out movieCategory))
            {
                movieCategory = mediaAccessor.RegisterMediaCategory(MEDIA_CATEGORY_NAME_MOVIE, new List<MediaCategory> { DefaultMediaCategories.Video });
            }
            MEDIA_CATEGORIES.Add(movieCategory);            
        }

        public NfoMovieMetadataExtractor()
        {
          ServiceRegistration.Get<ILogger>().Info("Creating MetadataExtractorMetadata instance");
          _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "NFO movie metadata extractor", MetadataExtractorPriority.External, false,
              MEDIA_CATEGORIES, new[]
                  {
                    MediaAspect.Metadata,
                    VideoAspect.Metadata,
                    MovieAspect.Metadata
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
                string nfoMoviePath1 = ProviderPathHelper.ChangeExtension(filePath, ".nfo");
                string nfoMoviePath2 = dirName + "/movie.nfo";
                
                if (ext != ".ts" && ext != ".mkv" && ext != ".avi" && ext != ".mp4" && ext != ".mpeg" && ext != ".mpg" && ext != ".flv")
                {
                    return false;
                }

                bool ok = false;

                ServiceRegistration.Get<ILogger>().Info("NFO movie metadata extractor running for {0}", filePath);

                //Movie data extraction
                List<String> paths = new List<String>();
                paths.Add(nfoMoviePath1);
                paths.Add(nfoMoviePath2);
                ok = TryExtractMovie(extractedAspectData, forceQuickMode, paths);

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

        protected XmlSerializer GetMovieXmlSerializer()
        {
            return _xmlSerializer ?? (_xmlSerializer = new XmlSerializer(typeof(Movie)));
        }

        private bool TryExtractMovie(IDictionary<Guid, MediaItemAspect> extractedAspectData, bool forceQuickMode, List<String> paths)
        {
            string path = null;

            try
            {
                /*
                 * Deserialization of xml data
                 */
                IResourceAccessor metaFileAccessor = null;
                foreach (string p in paths)
                {
                    if (ResourcePath.Deserialize(p).TryCreateLocalResourceAccessor(out metaFileAccessor))
                        path = p;
                }

                if (path == null || metaFileAccessor == null)
                    return false;

                Movie tags = null;
                using (metaFileAccessor)
                {
                    using (Stream metaStream = metaFileAccessor.OpenRead())
                    {
                        tags = (Movie)GetMovieXmlSerializer().Deserialize(metaStream);
                    }
                }

                if (tags == null)
                    return false;

                /*
                 * Initialize aspects
                 */
                MediaItemAspect mediaAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, MediaAspect.Metadata);
                MediaItemAspect videoAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, VideoAspect.Metadata);
                MediaItemAspect movieAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, MovieAspect.Metadata);

                /*
                 * Movie handling
                 */
                ServiceRegistration.Get<ILogger>().Info("Processing NFO data for movie {0}", tags);


                mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, tags.title);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE,);
                
                int year;
                if (int.TryParse(tags.year, out year))
                    mediaAspect.SetAttribute(MediaAspect.ATTR_RECORDINGTIME, new DateTime(year, 1, 1));

                //mediaAspect.SetAttribute(MediaAspect.ATTR_RATING, tags.rating);

                //mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT,);
                mediaAspect.SetAttribute(MediaAspect.ATTR_PLAYCOUNT, tags.playCount);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_LASTPLAYED,);

                string[] split = tags.genre.Split(new Char[] { ' ', ',', '.', ':' });
                List<String> genres = new List<String>();
                foreach (string s in split)
                {
                    if (s.Trim() != "")
                        genres.Add(s);
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_GENRES, genres);

                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_DURATION, tags.runtime);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_DIRECTOR, tags.director);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOSTREAMCOUNT,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOENCODING,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOBITRATE,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_AUDIOLANGUAGES,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_VIDEOENCODING,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_VIDEOBITRATE,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_WIDTH,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_HEIGHT,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ASPECTRATIO,);
                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_FPS,);

                List<String> actors = new List<String>();
                foreach (Actor a in tags.actor)
                {
                    actors.Add(a.name);
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ACTORS, actors);

                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ISDVD,);
                videoAspect.SetAttribute(VideoAspect.ATTR_STORYPLOT, tags.plot);


                movieAspect.SetAttribute(MovieAspect.ATTR_MOVIE_NAME, tags.title);
                movieAspect.SetAttribute(MovieAspect.ATTR_ORIG_MOVIE_NAME, tags.originaltitle);
                movieAspect.SetAttribute(MovieAspect.ATTR_IMDB_ID, tags.imdbId);
                //movieAspect.SetAttribute(MovieAspect.ATTR_TMDB_ID,);
                //movieAspect.SetAttribute(MovieAspect.ATTR_OFDB,);
                //movieAspect.SetAttribute(MovieAspect.ATTR_RUNTIME_M, tags.runtime);
                //movieAspect.SetAttribute(MovieAspect.ATTR_CERTIFICATION,);
                movieAspect.SetAttribute(MovieAspect.ATTR_TAGLINE, tags.tagline);
                //movieAspect.SetAttribute(MovieAspect.ATTR_POPULARITY,);
                //movieAspect.SetAttribute(MovieAspect.ATTR_BUDGET,);
                //movieAspect.SetAttribute(MovieAspect.ATTR_REVENUE,);

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


