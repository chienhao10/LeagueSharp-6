using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace StreamHelper
{
    struct CursorSprite
    {
        public Cursors Type;
        public Render.Sprite Sprite;
        public Render.Sprite SpriteCB;
        public bool hasCB;

        public CursorSprite(Cursors type, Bitmap bitmap, Bitmap cb = null)
        {
            Type = type;
            if (cb != null)
            {
                SpriteCB = new Render.Sprite(cb, new Vector2((-50000), (-50000)));
                SpriteCB.Add(0);
                SpriteCB.Visible = false;
                SpriteCB.OnDraw();
                hasCB = true;
            }
            else
            {
                SpriteCB = null;
                hasCB = false;
            }
            Sprite = new Render.Sprite(bitmap, new Vector2((-50000), (-50000)));
            Sprite.Add(0);
            Sprite.Visible = false;
            Sprite.OnDraw();
        }

        public void Enabled(bool enabled, bool cb)
        {
            Sprite.Visible = false;
            if (hasCB)
            {
                SpriteCB.Visible = false;
            }
            if (!enabled)
                return;
            if (cb && hasCB)
            {
                SpriteCB.Visible = true;
            }
            else
            {
                Sprite.Visible = true;
            }
        }

        public void SetPosition(Vector2 pos)
        {
            Sprite.Position = pos;
            if (hasCB)
            {
                SpriteCB.Position = pos;
            }
        }
    }

    enum Cursors
    {
        MoveTo,
        Attack,
        Shop,
        Turret,
        Normal,
        None
    }
}