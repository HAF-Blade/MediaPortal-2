#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Extensions.OnlineLibraries;
using MediaPortal.Extensions.UserServices.FanArtService.Interfaces;

namespace MediaPortal.Extensions.MetadataExtractors.SeriesMetadataExtractor.FanartProvider
{
  public class SeriesFanartProvider : IFanArtProvider
  {
    /// <summary>
    /// Gets a list of <see cref="FanArtImage"/>s for a requested <paramref name="mediaType"/>, <paramref name="fanArtType"/> and <paramref name="name"/>.
    /// The name can be: Series name, Actor name, Artist name depending on the <paramref name="mediaType"/>.
    /// </summary>
    /// <param name="mediaType">Requested FanArtMediaType</param>
    /// <param name="fanArtType">Requested FanArtType</param>
    /// <param name="name">Requested name of Series, Actor, Artist...</param>
    /// <param name="maxWidth">Maximum width for image. <c>0</c> returns image in original size.</param>
    /// <param name="maxHeight">Maximum height for image. <c>0</c> returns image in original size.</param>
    /// <param name="singleRandom">If <c>true</c> only one random image URI will be returned</param>
    /// <param name="result">Result if return code is <c>true</c>.</param>
    /// <returns><c>true</c> if at least one match was found.</returns>
    public bool TryGetFanArt(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, string name, int maxWidth, int maxHeight, bool singleRandom, out IList<string> result)
    {
      result = null;
      if (string.IsNullOrWhiteSpace(name))
        return false;

      int tvDbId;
      int seasonNum = 0;
      if (mediaType == FanArtConstants.FanArtMediaType.SeriesSeason)
      {
        int index = name.LastIndexOf(" S");
        if (!int.TryParse(name.Substring(index + 2), out seasonNum))
          return false;
        name = name.Substring(0, index);
      }

      string baseFolder = GetBaseFolder(mediaType, name, out tvDbId);
      // No known series
      if (baseFolder == null || !Directory.Exists(baseFolder))
        return false;

      string pattern = GetPattern(mediaType, fanArtType, name, tvDbId, seasonNum);
      if (string.IsNullOrEmpty(pattern))
        return false;

      try
      {
        DirectoryInfo directoryInfo = new DirectoryInfo(baseFolder);
        if (directoryInfo.Exists)
        {
          result = directoryInfo.GetFiles(pattern).Select(file => file.FullName).ToList();
          // If we tried to load season banners and did not find any, fallback to series banners
          if (mediaType == FanArtConstants.FanArtMediaType.SeriesSeason && result.Count == 0)
            return TryGetFanArt(FanArtConstants.FanArtMediaType.Series, fanArtType, name, maxWidth, maxHeight, singleRandom, out result);
          return result.Count > 0;
        }
      }
      catch (Exception) { }
      return false;
    }

    protected string GetPattern(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, string name, int tvdbId, int seasonNum)
    {
      if (mediaType == FanArtConstants.FanArtMediaType.Series)
      {
        switch (fanArtType)
        {
          case FanArtConstants.FanArtType.Banner:
            return "img_graphical_*.jpg";
          case FanArtConstants.FanArtType.Poster:
            return "img_posters_*.jpg";
          case FanArtConstants.FanArtType.FanArt:
            return "img_fan-*.jpg";
          default:
            return null;
        }
      }
      if (mediaType == FanArtConstants.FanArtMediaType.SeriesSeason)
      {
        switch (fanArtType)
        {
          case FanArtConstants.FanArtType.Banner:
            return string.Format("img_seasonswide_{0}-{1}*.jpg", tvdbId, seasonNum);
          case FanArtConstants.FanArtType.Poster:
            return string.Format("img_posters_{0}-{1}*.jpg", tvdbId, seasonNum);
          default:
            return null;
        }
      }
      return null;
    }

    protected string GetBaseFolder(FanArtConstants.FanArtMediaType mediaType, string name, out int tvDbId)
    {
      switch (mediaType)
      {
        case FanArtConstants.FanArtMediaType.SeriesSeason:
        case FanArtConstants.FanArtMediaType.Series:
          return !SeriesTvDbMatcher.Instance.TryGetTvDbId(name, out tvDbId) ? null : Path.Combine(SeriesTvDbMatcher.CACHE_PATH, tvDbId.ToString());
        default:
          tvDbId = 0;
          return null;
      }
    }
  }
}
