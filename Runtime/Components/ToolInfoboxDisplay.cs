/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Readymade.Building {
    /// <summary>
    /// A display that holds components for displaying a detailed information about a builder tool.
    /// </summary>
    [RequireComponent ( typeof ( Canvas ) )]
    public class ToolInfoboxDisplay : MonoBehaviour {
        [SerializeField]
        [Required]
        [Tooltip ( "The text to display the title or name of the tool." )]
        private TMP_Text title;

        [SerializeField]
        [Required]
        [Tooltip ( "The text to display the description of the tool." )]
        private TMP_Text description;

        [SerializeField]
        [Required]
        [Tooltip ( "The container to parent additional information widgets to." )]
        private RectTransform container;

        [SerializeField]
        [Required]
        [Tooltip ( "A large image to display a preview of the tool in action." )]
        private Image preview;

        /// <summary>
        /// The container to parent additional information widgets to.
        /// </summary>
        public RectTransform Container => container;

        /// <summary>
        /// The text do display the description of the tool.
        /// </summary>
        public TMP_Text Description => description;

        /// <summary>
        /// The text to display the title or name of the tool.
        /// </summary>
        public TMP_Text Title => title;

        /// <summary>
        /// The required canvas component at the root of this display.
        /// </summary>
        public Canvas Canvas => GetComponent<Canvas> ();

        /// <summary>
        /// A large image to display a preview of the tool in action.
        /// </summary>
        public Image Preview => preview;
    }
}