#region Copyright 2021-2023 C. Augusto Proiete & Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;
using Serilog.Debugging;

namespace Serilog.Sinks.RichTextBox.Abstraction
{
    internal class RichTextBoxImpl : IRichTextBox
    {
        private readonly DispatcherPriority _priority;
        private readonly System.Windows.Controls.RichTextBox[] _richTextBoxes;
        private readonly ILookup<Dispatcher, System.Windows.Controls.RichTextBox> _richTextBoxesByDispatcher;
        public RichTextBoxImpl(DispatcherPriority priority, params System.Windows.Controls.RichTextBox[] richTextBoxes)
        {
            _priority = priority;
            _richTextBoxes = richTextBoxes?.ToArray() ?? throw new ArgumentNullException(nameof(richTextBoxes));

            _richTextBoxesByDispatcher = _richTextBoxes.ToLookup(x => x.Dispatcher);
        }

        private void Write(List<string> xamlParagraphTexts,
                           IEnumerable<System.Windows.Controls.RichTextBox> richTextBoxes)
        {
            foreach (var richTextBox in richTextBoxes)
            {
                var parsedParagraphs = new List<Paragraph>();

                foreach (var xamlParagraphText in xamlParagraphTexts)
                {
                    try
                    {
                        var parsedParagraph = (Paragraph)XamlReader.Parse(xamlParagraphText);
                        parsedParagraphs.Add(parsedParagraph);

                    }
                    catch (XamlParseException ex)
                    {
                        SelfLog.WriteLine($"Error parsing `{xamlParagraphText}` to XAML: {ex.Message}");
                        throw;
                    }
                }
                
                var inlines = (
                                  from x in parsedParagraphs
                                  from y in x.Inlines
                                  select y
                              ).ToList();

                var flowDocument = richTextBox.Document ??= new FlowDocument();

                if (flowDocument.Blocks.LastBlock is Paragraph { } target)
                {
                    target.Inlines.AddRange(inlines);
                }
                else
                {
                    var paragraph = new Paragraph();
                    paragraph.Inlines.AddRange(inlines);

                    flowDocument.Blocks.Add(paragraph);
                }
            }
        }

        public async Task WriteAsync(List<string> xamlParagraphTexts)
        {
            foreach (var Pair in _richTextBoxesByDispatcher)
            {
                await Pair.Key.InvokeAsync(() => Write(xamlParagraphTexts, Pair), _priority).Task
                    .ConfigureAwait(false);
            }
        }
    }
}
