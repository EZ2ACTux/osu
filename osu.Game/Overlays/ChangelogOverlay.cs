﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Changelog;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public class ChangelogOverlay : FullscreenOverlay
    {
        private ChangelogHeader header;

        private UpdateStreamBadgeArea updateStreamBadges;

        private Container<ChangelogContent> content;

        private SampleChannel sampleBack;

        private List<APIChangelogBuild> builds;

        public readonly Bindable<APIChangelogBuild> Current = new Bindable<APIChangelogBuild>();

        public void ShowListing() => Current.Value = null;

        /// <summary>
        /// Fetches and shows a specific build from a specific update stream.
        /// </summary>
        /// <param name="build">Must contain at least <see cref="APIUpdateStream.Name"/> and
        /// <see cref="APIChangelogBuild.Version"/>. If <see cref="APIUpdateStream.DisplayName"/> and
        /// <see cref="APIChangelogBuild.DisplayVersion"/> are specified, the header will instantly display them.</param>
        public void ShowBuild([NotNull] APIChangelogBuild build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));

            Current.Value = build;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuColour colour)
        {
            Waves.FirstWaveColour = colour.GreyVioletLight;
            Waves.SecondWaveColour = colour.GreyViolet;
            Waves.ThirdWaveColour = colour.GreyVioletDark;
            Waves.FourthWaveColour = colour.GreyVioletDarker;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(49, 36, 54, 255),
                },
                new ScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = new ReverseChildIDFillFlowContainer<Drawable>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            header = new ChangelogHeader
                            {
                                ListingSelected = ShowListing,
                            },
                            updateStreamBadges = new UpdateStreamBadgeArea(),
                            content = new Container<ChangelogContent>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            }
                        },
                    },
                },
            };

            updateStreamBadges.Current.ValueChanged += e =>
            {
                if (e.NewValue?.LatestBuild != null && e.NewValue != Current.Value?.UpdateStream)
                    ShowBuild(e.NewValue.LatestBuild);
            };

            sampleBack = audio.Sample.Get(@"UI/generic-select-soft");

            header.Current.BindTo(Current);

            Current.BindValueChanged(e =>
            {
                if (e.NewValue != null)
                {
                    updateStreamBadges.Current.Value = e.NewValue.UpdateStream;

                    loadContent(new ChangelogSingleBuild(e.NewValue));
                }
                else
                {
                    updateStreamBadges.Current.Value = null;
                    loadContent(new ChangelogListing(builds));
                }
            });
        }

        public override bool OnPressed(GlobalAction action)
        {
            switch (action)
            {
                case GlobalAction.Back:
                    if (Current.Value == null)
                    {
                        State = Visibility.Hidden;
                    }
                    else
                    {
                        Current.Value = null;
                        sampleBack?.Play();
                    }

                    return true;
            }

            return false;
        }

        protected override void PopIn()
        {
            base.PopIn();

            if (!initialFetchPerformed)
                fetchListing();
        }

        private bool initialFetchPerformed;

        private void fetchListing()
        {
            initialFetchPerformed = true;

            var req = new GetChangelogRequest();
            req.Success += res =>
            {
                // remap streams to builds to ensure model equality
                res.Builds.ForEach(b => b.UpdateStream = res.Streams.Find(s => s.Id == b.UpdateStream.Id));
                res.Streams.ForEach(s => s.LatestBuild.UpdateStream = res.Streams.Find(s2 => s2.Id == s.LatestBuild.UpdateStream.Id));

                builds = res.Builds;
                updateStreamBadges.Populate(res.Streams);

                Current.TriggerChange();
            };
            req.Failure += _ => initialFetchPerformed = false;

            API.Queue(req);
        }

        private CancellationTokenSource loadContentTask;

        private void loadContent(ChangelogContent newContent)
        {
            content.FadeTo(0.2f, 300, Easing.OutQuint);

            loadContentTask?.Cancel();

            LoadComponentAsync(newContent, c =>
            {
                content.FadeIn(300, Easing.OutQuint);

                c.BuildSelected = ShowBuild;
                content.Child = c;
            }, (loadContentTask = new CancellationTokenSource()).Token);
        }
    }
}
