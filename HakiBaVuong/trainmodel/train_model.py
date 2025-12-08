
import pandas as pd
from lightfm import LightFM
from lightfm.data import Dataset
import pickle
from sqlalchemy import create_engine

DB_URL = (
    "mssql+pyodbc://DESKTOP-DTO0F6T"
    "?driver=ODBC+Driver+17+for+SQL+Server"
    "&trusted_connection=yes"
    "&database=HakiBaVuong"
)
print("Đang kết nối database (Windows Auth)...")
engine = create_engine(DB_URL)

print("Đang lấy dữ liệu Wishlist...")
df1 = pd.read_sql("SELECT CustomerId, ProductId FROM Wishlists", engine)
df1['weight'] = 10

print("Đang lấy dữ liệu ViewedProducts...")
df2 = pd.read_sql("SELECT CustomerId, ProductId FROM ViewedProducts", engine)
df2['weight'] = 3

print("Đang lấy dữ liệu Orders...")
df3 = pd.read_sql("""
    SELECT o.CustomerId, oi.ProductId 
    FROM Orders o 
    JOIN OrderItems oi ON o.OrderId = oi.OrderId 
    WHERE o.Status = 'Hoàn thành'
""", engine)
df3['weight'] = 20

# Gộp + bỏ trùng
data = pd.concat([df1, df2, df3[['CustomerId','ProductId','weight']]], ignore_index=True)
data = data.drop_duplicates(['CustomerId','ProductId'])

print(f"Total tương tác: {len(data)} khách x sản phẩm")

if len(data) == 0:
    print("KHÔNG CÓ DỮ LIỆU – KIỂM TRA DATABASE HOẶC TÊN BẢNG!")
    exit()

# Train model
dataset = Dataset()
dataset.fit(data['CustomerId'].unique(), data['ProductId'].unique())

(interactions, weights) = dataset.build_interactions(
    [(row.CustomerId, row.ProductId, row.weight) for _, row in data.iterrows()]
)

model = LightFM(no_components=64, loss='warp', learning_rate=0.03, random_state=2025)
print("Bắt đầu training... (30-90 giây)")
model.fit(weights, epochs=30, num_threads=8, verbose=True)

# Lưu model
save_data = {'model': model, 'dataset': dataset}
with open('lightfm_model.pkl', 'wb') as f:
    pickle.dump(save_data, f)

print("TRAIN XONG 100%!")
print(f"→ Số user: {len(dataset.mapping()[0])}")
print(f"→ Số item: {len(dataset.mapping()[2])}")
