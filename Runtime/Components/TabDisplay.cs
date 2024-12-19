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

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Readymade.Building.Components {
    /// <summary>
    /// A display holding references to components for displaying a tab with an optional tooltip. 
    /// </summary>
    public class TabDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDeselectHandler, ISelectHandler {
        [Tooltip ( "The toggle that captures input events and displays feedback." )]
        [SerializeField]
        public Toggle toggle;

        [Tooltip ( "The label text on the toggle." )]
        [SerializeField]
        public TMP_Text label;

        [Tooltip ( "The index text on the toggle." )]
        [SerializeField]
        public TMP_Text index;

        [Tooltip ( "The canvas that holds the tooltip." )]
        [SerializeField]
        public Canvas tipCanvas;

        [Tooltip ( "The text to display the tooltip." )]
        [SerializeField]
        public TMP_Text tipText;

        [Tooltip ( "The label to display the tooltip title." )]
        [SerializeField]
        public TMP_Text tipLabel;

        [Tooltip ( "The image to display the icon." )]
        [SerializeField]
        public Image iconImage;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start () {
            if ( tipCanvas ) {
                tipCanvas.enabled = false;
            }
        }

        /// <summary>
        /// Sets the icon to display on the button.
        /// Hides the label and index objects.
        /// </summary>
        /// <param name="icon">The icon.</param>
        public void SetIcon ( Sprite icon ) {
            if ( icon != null ) {
                iconImage.sprite = icon;
            }
        }

        /// <summary>
        /// Sets the visibility of the icon.
        /// </summary>
        /// <param name="isOn"></param>
        public void ShowIcon ( bool isOn ) {
            iconImage.gameObject.SetActive ( iconImage && iconImage.sprite && isOn );
        }

        /// <summary>
        /// Sets the visibility of the label and index text.
        /// </summary>
        /// <param name="isOn"></param>
        public void ShowLabels ( bool isOn ) {
            if ( label ) {
                label.gameObject.SetActive ( isOn );
            }

            if ( index ) {
                index.gameObject.SetActive ( isOn );
            }
        }

        /// <inheritdoc />
        public void OnPointerEnter ( PointerEventData eventData ) {
            if ( tipCanvas ) {
                tipCanvas.enabled = true;
            }
        }

        /// <inheritdoc />
        public void OnPointerExit ( PointerEventData eventData ) {
            if ( tipCanvas ) {
                tipCanvas.enabled = false;
            }
        }

        /// <inheritdoc />
        public void OnSelect ( BaseEventData eventData ) {
            if ( tipCanvas ) {
                tipCanvas.enabled = true;
            }
        }

        /// <inheritdoc />
        public void OnDeselect ( BaseEventData eventData ) {
            if ( tipCanvas ) {
                tipCanvas.enabled = false;
            }
        }
    }
}