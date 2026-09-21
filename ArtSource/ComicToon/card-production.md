# Comic mutation cards

Approved design: angular purple comic page, empty illustration panel, bottom rarity strip. The production background contains no baked labels, numbers, arrow or icon. Source image was generated with the built-in imagegen tool; the original is preserved in the generated_images folder as exec-31fd9ee8-5c9f-4041-b6ee-461c73b0be51.png.

Runtime art: Assets/Art/UI/ComicToon/Mutation Card.png and Mutation Card Sprite.asset. UI/ComicCard remaps the lavender body and footer separately from yellow banners and edge accents. Four shared materials provide Common gray, Rare cyan, Epic purple and Legendary gold with dark warm banners. The shared texture avoids four duplicate raster sheets.

All labels use ComicUITheme.headingFont and outlinedText, matching the existing game. Bonus labels retain the actual rolled TileCardOffer values. The old/new example comparison has been removed. The description names the affected stat, and the lower panel shows only the actual rolled bonus amount with its correct percentage/flat unit. Multi-stat offers keep all named bonus lines. No upgrade balance, card pool weights, rarity rolls or selection rules were changed.

ComicHoverMotion provides paused-time hover feedback; ComicCardSparkles draws a small number of hard-edged, ink-outlined glints, color/count based on rarity, only on enabled cards. Common has hover feedback without ambient glints. The illustration area now contains a lightweight ComicTilePreview mesh: an outlined square tile with cel-shaded edges and a hard angular cast shadow. Its face uses the actual offered TileModifierSO.tileColor, independently of rarity. A small UI Shadow offsets the card silhouette without replacing its artwork. The preview never receives pointer raycasts.

ComicCardBuilder.RunBatch updates the existing prefab and GameScene slots. ComicCardVerification.RunBatch checks four palettes, shared font material, real rolled values, single- and multi-stat bonus labels, callback identity, paused hover, actual grid application and duplicate-click prevention in an isolated Unity project. Backups: Backups/CardComic-20260920.
