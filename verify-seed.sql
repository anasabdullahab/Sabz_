SELECT u.Email, f.FarmName, c.CropName, c.PlantingDate, c.CropCatalogId AS Catalog, COUNT(mc.Id) AS Checks,
       SUM(CASE WHEN mc.ScheduledDate <= SYSUTCDATETIME() THEN 1 ELSE 0 END) AS DueNow
FROM Crops c
JOIN Farms f ON c.FarmId = f.Id
JOIN Users u ON f.UserId = u.Id
LEFT JOIN CropMonitoringChecks mc ON mc.CropId = c.Id
GROUP BY u.Email, f.FarmName, c.CropName, c.PlantingDate, c.CropCatalogId
ORDER BY u.Email, Checks DESC;

SELECT COUNT(*) AS TotalChecks FROM CropMonitoringChecks;

SELECT COUNT(*) AS TotalNotifs, SUM(CASE WHEN IsRead=0 THEN 1 ELSE 0 END) AS Unread FROM Notifications;

SELECT p.Content, u.FullName FROM CommunityPosts p JOIN Users u ON p.UserId = u.Id WHERE p.IsDeleted = 0 ORDER BY p.CreatedAt;
