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

    public class NfoMusicVideoMetadataExtractor : IMetadataExtractor
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

        [XmlRoot(ElementName = "musicvideo")]
        public class MusicVideo
        {
            [XmlElement(ElementName = "title")]
            public string title { get; set; }

            [XmlElement(ElementName = "artist")]
            public string artist { get; set; }

            [XmlElement(ElementName = "album")]
            public string album { get; set; }

            [XmlElement(ElementName = "genre")]
            public string genre { get; set; }

            [XmlElement(ElementName = "runtime")]
            public string runtime { get; set; }

            [XmlElement(ElementName = "plot")]
            public string plot { get; set; }

            [XmlElement(ElementName = "year")]
            public int year { get; set; }

            [XmlElement(ElementName = "director")]
            public string director { get; set; }

            [XmlElement(ElementName = "studio")]
            public string studio { get; set; }
        }

        #endregion

        #region Constants
        /// <summary>
        /// GUID string for the NFO metadata extractor.
        /// </summary>
        public const string METADATAEXTRACTOR_ID_STR = "EC0684E1-C9D0-4F53-AED7-E7FDF567DE9D";

        /// <summary>
        /// Tve3 metadata extractor GUID.
        /// </summary>
        public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);

        #endregion

        #region Class variables
        protected MetadataExtractorMetadata _metadata; //Metadata which has been found
        protected static IList<MediaCategory> MEDIA_CATEGORIES = new List<MediaCategory>();
        protected static XmlSerializer _xmlSerializer = null; // Lazy initialized
        #endregion

        #region Constructors
        static NfoMusicVideoMetadataExtractor()
        {
            MEDIA_CATEGORIES.Add(DefaultMediaCategories.Video);
            MEDIA_CATEGORIES.Add(DefaultMediaCategories.Audio);

            IMediaAccessor mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();      
        }

        public NfoMusicVideoMetadataExtractor()
        {
          ServiceRegistration.Get<ILogger>().Info("Creating MetadataExtractorMetadata instance");
          _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "NFO music video metadata extractor", MetadataExtractorPriority.External, false,
              MEDIA_CATEGORIES, new[]
                  {
                    MediaAspect.Metadata,
                    VideoAspect.Metadata,
                    AudioAspect.Metadata
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

            ServiceRegistration.Get<ILogger>().Info("NFO music video metadata extractor running");

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
                string nfoMusicPath1 = ProviderPathHelper.ChangeExtension(filePath, ".nfo");

                if (ext != ".ts" && ext != ".mkv" && ext != ".avi" && ext != ".mp4" && ext != ".mpeg" && ext != ".mpg" && ext != ".flv")
                {
                    return false;
                }

                bool ok = false;

                ServiceRegistration.Get<ILogger>().Info("NFO music video metadata extractor running for {0}", filePath);

                //Music Video data extraction
                List<String> paths = new List<String>();
                paths.Add(nfoMusicPath1);
                ok = TryExtractMusicVideo(extractedAspectData, forceQuickMode, paths);

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

        protected XmlSerializer GetMusicVideoXmlSerializer()
        {
            return _xmlSerializer ?? (_xmlSerializer = new XmlSerializer(typeof(MusicVideo)));
        }

        private bool TryExtractMusicVideo(IDictionary<Guid, MediaItemAspect> extractedAspectData, bool forceQuickMode, List<String> paths)
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

                MusicVideo tags = null;
                using (metaFileAccessor)
                {
                    using (Stream metaStream = metaFileAccessor.OpenRead())
                    {
                        tags = (MusicVideo)GetMusicVideoXmlSerializer().Deserialize(metaStream);
                    }
                }

                if (tags == null)
                    return false;

                /*
                 * Initialize aspects
                 */
                MediaItemAspect mediaAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, MediaAspect.Metadata);
                MediaItemAspect videoAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, VideoAspect.Metadata);
                MediaItemAspect audioAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, AudioAspect.Metadata);

                /*
                 * Music Video data handling
                 */
                ServiceRegistration.Get<ILogger>().Info("Processing NFO data for music video {0}", tags);


                mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, tags.title);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE,);
                mediaAspect.SetAttribute(MediaAspect.ATTR_RECORDINGTIME, new DateTime(tags.year, 1, 1));
                //mediaAspect.SetAttribute(MediaAspect.ATTR_RATING,);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT,);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_PLAYCOUNT,);
                //mediaAspect.SetAttribute(MediaAspect.ATTR_LASTPLAYED,);
                
                /* No Video genre for music videos
                string[] split = tags.genre.Split(new Char[] { ' ', ',', '.', ':' });
                List<String> genres = new List<String>();
                foreach (string s in split)
                {
                    if (s.Trim() != "")
                        genres.Add(s);
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_GENRES, genres);
                */ 
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

                /* No actors for music videos
                List<String> actors = new List<String>();
                foreach (Actor a in tags.actor)
                {
                    actors.Add(a.name);
                }
                videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ACTORS, actors);
                */

                //videoAspect.SetCollectionAttribute(VideoAspect.ATTR_ISDVD,);
                videoAspect.SetAttribute(VideoAspect.ATTR_STORYPLOT, tags.plot);


                audioAspect.SetCollectionAttribute(AudioAspect.ATTR_ARTISTS, new List<String> { tags.artist });
                audioAspect.SetAttribute(AudioAspect.ATTR_ALBUM, tags.album);

                string[] split = tags.genre.Split(new Char[] { ' ', ',', '.', ':' });
                List<String> genres = new List<String>();
                foreach (string s in split)
                {
                    if (s.Trim() != "")
                        genres.Add(s);
                }
                audioAspect.SetCollectionAttribute(AudioAspect.ATTR_GENRES, genres);

                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_DURATION, tags.runtime);
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_TRACK, tags.title);
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_NUMTRACK,);

                List<String> albumArtists = new List<String>();
                albumArtists.Add(tags.artist);
                audioAspect.SetCollectionAttribute(AudioAspect.ATTR_ALBUMARTISTS, albumArtists);

                List<String> composers = new List<String>();
                composers.Add(tags.studio);
                audioAspect.SetCollectionAttribute(AudioAspect.ATTR_COMPOSERS, composers);
                
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_ENCODING,);
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_BITRATE,);
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_DISCID,);
                //audioAspect.SetCollectionAttribute(AudioAspect.ATTR_NUMDISCS,);

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


