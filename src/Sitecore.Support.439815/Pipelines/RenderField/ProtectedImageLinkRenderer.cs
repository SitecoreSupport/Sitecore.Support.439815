namespace Sitecore.Support.Pipelines.RenderField
{
  using System;
  using System.Text;
  using Sitecore;
  using Sitecore.Configuration;
  using Sitecore.Diagnostics;
  using Sitecore.Pipelines.RenderField;
  using Sitecore.Resources.Media;

  /// <summary>
  /// Protected Image Link Renderer class
  /// </summary>
  public class ProtectedImageLinkRenderer
  {
    /// <summary>
    /// The quotes
    /// </summary>
    private readonly char[] quotes = new[]
    {
      '\'', '\"'
    };

    /// <summary>
    /// Runs the processor.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void Process([NotNull] RenderFieldArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      if (!Settings.Media.RequestProtection.Enabled)
      {
        return;
      }

      if (args.FieldTypeKey.StartsWith("__"))
      {
        return;
      }

      args.Result.FirstPart = this.HashImageReferences(args.Result.FirstPart);
      args.Result.LastPart = this.HashImageReferences(args.Result.LastPart);
    }

    /// <summary>
    /// Gets the protected URL.
    /// </summary>
    /// <param name="url">The URL to protect.</param>
    /// <returns>The protected by hash parameter URL.</returns>
    [NotNull]
    protected virtual string GetProtectedUrl([NotNull]string url)
    {
      Assert.IsNotNull(url, "url");
      return HashingUtils.ProtectAssetUrl(url);
    }

    /// <summary>
    /// Hashes the image references.
    /// </summary>
    /// <param name="renderedText">The rendered text.</param>
    /// <returns>Fixed image references</returns>
    [NotNull]
    protected string HashImageReferences([NotNull] string renderedText)
    {
      Assert.ArgumentNotNull(renderedText, "renderedText");
      if (renderedText.IndexOf("<img ", StringComparison.OrdinalIgnoreCase) < 0)
      {
        return renderedText;
      }

      // Check if images contain parameters to minimize the performance impact.
      int index = 0;
      bool containsParameters = false;
      while (index < renderedText.Length && !containsParameters)
      {
        var tagIndex = renderedText.IndexOf("<img", index, StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0)
        {
          break;
        }

        containsParameters = this.CheckReferenceForParams(renderedText, tagIndex);
        var tagCloseIndex = renderedText.IndexOf(">", tagIndex, StringComparison.OrdinalIgnoreCase) + 1;
        index = tagCloseIndex;
      }

      if (!containsParameters)
      {
        return renderedText;
      }

      index = 0;
      var buffer = new StringBuilder(renderedText.Length + 128);
      while (index < renderedText.Length)
      {
        var tagIndex = renderedText.IndexOf("<img", index, StringComparison.OrdinalIgnoreCase);
        if (tagIndex > -1)
        {
          var tagCloseIndex = renderedText.IndexOf(">", tagIndex, StringComparison.OrdinalIgnoreCase) + 1;
          buffer.Append(renderedText.Substring(index, tagIndex - index));
          string imgTag = renderedText.Substring(tagIndex, tagCloseIndex - tagIndex);
          buffer.Append(this.ReplaceReference(imgTag));
          index = tagCloseIndex;
        }
        else
        {
          buffer.Append(renderedText.Substring(index, renderedText.Length - index));
          index = int.MaxValue;
        }
      }

      return buffer.ToString();
    }

    /// <summary>
    /// Checks the reference for parameters.
    /// </summary>
    /// <param name="renderedText">The rendered text.</param>
    /// <param name="tagStart">The tag start.</param>
    /// <returns><c>True</c> is reference contains dangerous parameters, <c>false</c> otherwise</returns>
    protected bool CheckReferenceForParams([NotNull] string renderedText, int tagStart)
    {
      Assert.ArgumentNotNull(renderedText, "renderedText");
      renderedText = renderedText.Replace("&amp;", "&");
      int urlStartindex = renderedText.IndexOf("src", tagStart, StringComparison.OrdinalIgnoreCase) + 3;
      urlStartindex = renderedText.IndexOfAny(this.quotes, urlStartindex) + 1;
      int urlEndIndex = renderedText.IndexOfAny(this.quotes, urlStartindex);

      int paramIndex = renderedText.IndexOf('?', urlStartindex, urlEndIndex - urlStartindex);
      if (paramIndex < 0)
      {
        // no parameters, no need to go through the whole list
        return false;
      }

      // check if abusable parameters present
      return ContainsUnsafeParametersInQuery(renderedText.Substring(paramIndex, urlEndIndex - paramIndex));
    }

    /// <summary>
    /// Determines whether specified URL query parameters contain parameters to protect.
    /// </summary>
    /// <param name="urlParameters">The URL parameters.</param>
    /// <returns>
    ///   <c>true</c> if specified URL query parameters contain parameters to protect; otherwise, <c>false</c>.
    /// </returns>
    protected virtual bool ContainsUnsafeParametersInQuery(string urlParameters)
    {
      return !HashingUtils.IsSafeUrl(urlParameters);
    }

    /// <summary>
    /// Replaces the reference.
    /// </summary>
    /// <param name="imgTag">The <c>img</c> tag.</param>
    /// <returns>Fixed image reference</returns>
    [NotNull]
    private string ReplaceReference([NotNull] string imgTag)
    {
      Assert.ArgumentNotNull(imgTag, "imgTag");
      bool imgContainsEscapedAmp = true;
      string unescapedImgTag = imgTag; 
   
      if (imgTag.Contains("&amp;"))
      {
        unescapedImgTag = unescapedImgTag.Replace("&amp;", "&");
      }
      else if (imgTag.Contains("&"))
      {
        imgContainsEscapedAmp = false;
      }

      int urlStartindex = unescapedImgTag.IndexOf("src", StringComparison.OrdinalIgnoreCase) + 3;
      urlStartindex = unescapedImgTag.IndexOfAny(this.quotes, urlStartindex) + 1;
      int urlEndIndex = unescapedImgTag.IndexOfAny(this.quotes, urlStartindex);
      string url = unescapedImgTag.Substring(urlStartindex, urlEndIndex - urlStartindex);
      if (!url.Contains("?"))
      {
        return imgTag; // no parameters, no need to arm the URL;
      }

      url = this.GetProtectedUrl(url);
      if (imgContainsEscapedAmp)
      {
        url = url.Replace("&", "&amp;");
      }

      return unescapedImgTag.Substring(0, urlStartindex) + url + unescapedImgTag.Substring(urlEndIndex, unescapedImgTag.Length - urlEndIndex);
    }
  }
}