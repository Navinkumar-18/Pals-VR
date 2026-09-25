using System.Collections;
using System.Collections.Generic;
using System.Text;
using AmbedkarHeritage.Core;
using AmbedkarHeritage.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Reusable world-space document viewer. Displays image pages (from
    /// record.pages / image / document references) or text pages (transcripts /
    /// OCR output / metadata) with zoom, previous/next paging and close. Long
    /// OCR transcripts are split into multiple readable text pages
    /// (Phase E: READ OCR TEXT). Texture loading is asynchronous where possible
    /// and never loads more than one texture at a time. Missing media shows an
    /// honest placeholder page instead of failing.
    /// </summary>
    public sealed class DocumentViewer : MonoBehaviour
    {
        /// <summary>Max characters per text page — keeps VR text readable.</summary>
        public const int TextPageChars = 2200;
        public sealed class ViewerPage
        {
            public enum Kind { Image, Text }

            public Kind kind;
            public string reference; // asset path / URI for image pages
            public string text;      // body text for text pages
        }

        public static DocumentViewer Instance { get; private set; }

        [SerializeField]
        private Text headerText;

        [SerializeField]
        private Text pageCounterText;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private RawImage pageImage;

        [SerializeField]
        private Text pageText;

        [SerializeField]
        private VRButton zoomInButton;

        [SerializeField]
        private VRButton zoomOutButton;

        [SerializeField]
        private VRButton prevButton;

        [SerializeField]
        private VRButton nextButton;

        [SerializeField]
        private VRButton closeButton;

        private List<ViewerPage> _pages = new List<ViewerPage>();
        private int _index;
        private float _zoom = 1f;

        private void Awake()
        {
            Instance = this;
        }

        public int PageCount
        {
            get { return _pages != null ? _pages.Count : 0; }
        }

        public bool HasZoom
        {
            get { return zoomInButton != null && zoomOutButton != null; }
        }

        public bool IsWired
        {
            get { return zoomInButton != null && zoomOutButton != null && prevButton != null && nextButton != null && closeButton != null; }
        }

        public void Configure(Text header, Text counter, RectTransform content, RawImage image, Text text,
            VRButton zoomIn, VRButton zoomOut, VRButton prev, VRButton next, VRButton close)
        {
            headerText = header;
            pageCounterText = counter;
            contentRoot = content;
            pageImage = image;
            pageText = text;
            zoomInButton = zoomIn;
            zoomOutButton = zoomOut;
            prevButton = prev;
            nextButton = next;
            closeButton = close;

            if (zoomInButton != null) zoomInButton.SetAction(ZoomIn);
            if (zoomOutButton != null) zoomOutButton.SetAction(ZoomOut);
            if (prevButton != null) prevButton.SetAction(PreviousPage);
            if (nextButton != null) nextButton.SetAction(NextPage);
            if (closeButton != null) closeButton.SetAction(Close);
        }

        public void Open(ArchiveRecord record, bool startInText)
        {
            if (record == null)
            {
                return;
            }

            _pages = PlanPages(record);
            _index = 0;

            if (startInText)
            {
                int firstText = FirstTextIndex();
                _index = firstText >= 0 ? firstText : 0;
            }

            if (headerText != null)
            {
                headerText.text = record.title ?? record.id;
            }

            gameObject.SetActive(true);
            ShowPage(_index);
        }

        public void Close()
        {
            gameObject.SetActive(false);

            if (pageImage != null && pageImage.texture != null)
            {
                // Only one texture is held at a time; release it on close.
                pageImage.texture = null;
            }
        }

        public void ZoomIn()
        {
            SetZoom(_zoom + 0.25f);
        }

        public void ZoomOut()
        {
            SetZoom(_zoom - 0.25f);
        }

        private void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, 0.5f, 2.5f);
            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one * _zoom;
            }
        }

        public void NextPage()
        {
            if (_pages.Count == 0)
            {
                return;
            }

            _index = Mathf.Min(_index + 1, _pages.Count - 1);
            ShowPage(_index);
        }

        public void PreviousPage()
        {
            if (_pages.Count == 0)
            {
                return;
            }

            _index = Mathf.Max(_index - 1, 0);
            ShowPage(_index);
        }

        private int FirstTextIndex()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                if (_pages[i].kind == ViewerPage.Kind.Text)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ShowPage(int index)
        {
            if (_pages == null || index < 0 || index >= _pages.Count)
            {
                return;
            }

            SetZoom(1f);
            _index = index;
            ViewerPage page = _pages[index];

            if (pageCounterText != null)
            {
                pageCounterText.text = "PAGE " + (_index + 1) + " / " + _pages.Count;
            }

            if (pageImage != null && pageImage.texture != null)
            {
                pageImage.texture = null;
            }

            if (page.kind == ViewerPage.Kind.Text)
            {
                ShowTextPage(page.text);
            }
            else
            {
                LoadImagePage(page.reference);
            }
        }

        private void ShowTextPage(string body)
        {
            if (pageImage != null) pageImage.gameObject.SetActive(false);
            if (pageText != null)
            {
                pageText.gameObject.SetActive(true);
                pageText.text = body ?? "";
            }
        }

        private void LoadImagePage(string reference)
        {
            Texture2D cached = Resources.Load<Texture2D>(ToResourcePath(reference));
            if (cached != null)
            {
                ApplyImage(cached);
                return;
            }

            if (Application.isPlaying)
            {
                StartCoroutine(LoadAsync(reference));
            }
            else
            {
                ShowImageFallback(reference);
            }
        }

        private IEnumerator LoadAsync(string reference)
        {
            ResourceRequest request = Resources.LoadAsync<Texture2D>(ToResourcePath(reference));
            yield return request;

            Texture2D texture = request.asset as Texture2D;
            if (texture != null)
            {
                ApplyImage(texture);
            }
            else
            {
                ShowImageFallback(reference);
            }
        }

        private void ApplyImage(Texture2D texture)
        {
            if (pageText != null) pageText.gameObject.SetActive(false);
            if (pageImage != null)
            {
                pageImage.gameObject.SetActive(true);
                pageImage.texture = texture;
            }
        }

        private void ShowImageFallback(string reference)
        {
            if (pageImage != null) pageImage.gameObject.SetActive(false);
            if (pageText != null)
            {
                pageText.gameObject.SetActive(true);
                pageText.text = "IMAGE UNAVAILABLE IN DEMO MODE" + System.Environment.NewLine + System.Environment.NewLine
                                + "Page reference: " + reference + System.Environment.NewLine + System.Environment.NewLine
                                + "Archive scans will be streamed by the backend asset pipeline in a later phase.";
            }
        }

        private static string ToResourcePath(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return "";
            }

            string path = reference.Trim().TrimStart('/');
            if (path.StartsWith("Assets/Resources/"))
            {
                path = path.Substring("Assets/Resources/".Length);
            }

            if (System.IO.Path.HasExtension(path))
            {
                path = path.Substring(0, path.Length - System.IO.Path.GetExtension(path).Length);
            }

            return path;
        }

        /// <summary>
        /// Builds the page list for a record purely from data. Image pages come
        /// from record.pages (multi-page support), falling back to image/document
        /// references. A text page always follows with real metadata plus the
        /// OCR transcript when present — never fabricated content.
        /// </summary>
        public static List<ViewerPage> PlanPages(ArchiveRecord record)
        {
            List<ViewerPage> pages = new List<ViewerPage>();
            if (record == null)
            {
                pages.Add(new ViewerPage { kind = ViewerPage.Kind.Text, text = "No record available." });
                return pages;
            }

            if (record.pages != null && record.pages.Count > 0)
            {
                for (int i = 0; i < record.pages.Count; i++)
                {
                    pages.Add(new ViewerPage { kind = ViewerPage.Kind.Image, reference = record.pages[i] });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(record.image))
                {
                    pages.Add(new ViewerPage { kind = ViewerPage.Kind.Image, reference = record.image });
                }

                if (!string.IsNullOrEmpty(record.document))
                {
                    pages.Add(new ViewerPage { kind = ViewerPage.Kind.Image, reference = record.document });
                }
            }

            if (!string.IsNullOrEmpty(record.video))
            {
                pages.Add(new ViewerPage
                {
                    kind = ViewerPage.Kind.Text,
                    text = "VIDEO RECORD" + System.Environment.NewLine + System.Environment.NewLine
                           + "Playback will stream from the archive backend in a later phase." + System.Environment.NewLine
                           + "Media reference: " + record.video
                });
            }

            StringBuilder body = new StringBuilder();
            body.Append(record.title ?? record.id).Append(System.Environment.NewLine);
            body.Append("Date: ").Append(string.IsNullOrEmpty(record.date) ? "n/a" : record.date)
                .Append("   |   Category: ").Append(string.IsNullOrEmpty(record.category) ? "n/a" : record.category)
                .Append(System.Environment.NewLine);
            body.Append("Type: ").Append(record.type).Append("   |   Language: ").Append(record.language)
                .Append("   |   Document ID: ").Append(record.id).Append(System.Environment.NewLine);
            body.Append(System.Environment.NewLine);

            if (!string.IsNullOrEmpty(record.ocrText))
            {
                body.Append(record.ocrText);
            }
            else
            {
                body.Append("No transcript/OCR text for this demo record. The OCR pipeline will add text in a later phase.");
            }

            // Long transcripts are split into multiple readable text pages.
            pages.AddRange(SplitIntoTextPages(body.ToString()));

            return pages;
        }

        /// <summary>
        /// Splits a (possibly very long) text page body into one or more
        /// readable text pages (Phase E: READ OCR TEXT pagination). Short text
        /// stays on a single page — fully backward compatible with the original
        /// single trailing text page.
        /// </summary>
        public static List<ViewerPage> SplitIntoTextPages(string fullText)
        {
            List<ViewerPage> result = new List<ViewerPage>();
            if (string.IsNullOrEmpty(fullText))
            {
                result.Add(new ViewerPage { kind = ViewerPage.Kind.Text, text = "" });
                return result;
            }

            if (fullText.Length <= TextPageChars)
            {
                result.Add(new ViewerPage { kind = ViewerPage.Kind.Text, text = fullText });
                return result;
            }

            // Chunk on line boundaries when possible so OCR paragraphs are not
            // sliced mid-sentence; any chunk that is still over budget after a
            // pass (e.g. one enormous single line) is hard-split to the cap.
            string[] lines = fullText.Split('\n');
            List<string> chunks = new List<string>();
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = i == 0 ? lines[i] : "\n" + lines[i];
                if (current.Length > 0 && current.Length + line.Length > TextPageChars)
                {
                    chunks.Add(current.ToString());
                    current.Length = 0;
                }

                current.Append(line);
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].Length <= TextPageChars)
                {
                    continue;
                }

                string overflow = chunks[i];
                chunks[i] = overflow.Substring(0, TextPageChars);
                chunks.Insert(i + 1, overflow.Substring(TextPageChars));
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                string text = chunks[i];
                if (chunks.Count > 1)
                {
                    // Mark OCR sub-pages so the reader knows where they are.
                    text = "OCR TEXT - PART " + (i + 1) + " / " + chunks.Count
                           + System.Environment.NewLine + System.Environment.NewLine + text;
                }

                result.Add(new ViewerPage { kind = ViewerPage.Kind.Text, text = text });
            }

            return result;
        }
    }
}