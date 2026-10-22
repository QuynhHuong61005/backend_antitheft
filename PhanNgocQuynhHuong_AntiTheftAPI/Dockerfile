# Sử dụng .NET SDK 8.0 để build ứng dụng
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy các file và khôi phục (restore) các package
COPY . ./
RUN dotnet restore

# Build và xuất bản (publish) ứng dụng dạng Release
RUN dotnet publish -c Release -o out

# Sử dụng .NET Runtime nhỏ gọn để chạy ứng dụng
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .

# Cấu hình để ứng dụng lắng nghe trên cổng (Port) mà Cloud cung cấp
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PhanNgocQuynhHuong_AntiTheftAPI.dll"]
